#include "message_handler.hpp"
#include "debug.hpp"
#include "json.hpp"
#include "message_type.hpp"
#include "message_sender.hpp"
#include "vector2.hpp"
#include "slither_manager.hpp"

#include <arpa/inet.h>
#include <string>

using json = nlohmann::json;

bool message_handler::extract_message(int id, int32_t& type, std::string& body, ServerContext& ctx){
	dbg_tag("SEQUENCE", "[message_handler::extract_message] id : ", id);
	ClientContext& client = ctx.client_contexts[id];
	auto& buffer = client.recv_buffer;
	int32_t body_len;
	int32_t header_len = sizeof(type)+sizeof(body_len);

	if(buffer.size() < header_len) return false;

	memcpy(&type, buffer.data(), sizeof(type));
	type = ntohl(type);

	memcpy(&body_len, buffer.data()+sizeof(type), sizeof(body_len));
	body_len = ntohl(body_len);

	if (body_len < 0 || body_len > 10000) {
		std::cerr << "[extract_message] Invalid body_len: " << body_len << std::endl;
		return false;
	}

	if(buffer.size() < header_len+body_len) return false;

	body.assign(buffer.begin()+header_len, buffer.begin()+header_len+body_len);
	buffer.erase(buffer.begin(), buffer.begin()+header_len+body_len);
    return true;
}

// server.cpp의 handle_read에서 호출
void message_handler::handle_message(
	Server& server,
	int id, 
	int32_t type, 
	const std::string& body, 
	ServerContext& ctx)
{
	dbg_tag("SEQUENCE", "[message_handler::handle_message]");
	switch (type)
	{
		case MSG_PLAYER_JOIN:
			message_handler::handle_player_join(id, body, ctx);
			break;
		
		case MSG_PLAYER_LEFT:
			message_handler::handle_player_left(id, body, ctx);
			break;

		case MSG_POS:
			message_handler::handle_pos(body);
			break;

		case MSG_ANGLE:
			message_handler::handle_angle(id, body, ctx);
			break;

		case MSG_CHAT:
			message_handler::handle_chat(id, body, ctx);
			break;

		case MSG_HIT:
			message_handler::handle_hit(id, body, ctx);
			break;

		case MSG_EAT:
			message_handler::handle_eat(id, body, ctx);
			break;
	}
}

void message_handler::handle_player_join(
	int id, 
	const std::string& body, 
	ServerContext& ctx)
{
	dbg_tag("SEQUENCE", "[message_handler::handle_player_join] id : ", id);
	// body 안에는 아무것도 없음 (0byte)
	// 정보를 설정해주고 id를 붙여서 다른 플레이어들에게 전송.
	// 본인 정보와 다른 모든 플레이어들의 정보를 join한 플레이어에게 전송.

	json j;
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);

		ClientContext& client = ctx.client_contexts[id];		
		client.id = id;
		client.head.x = rand() % 10 - 5;
		client.head.y = rand() % 10 - 5;
		client.turn_dir = "w";
		client.angle = M_PI / 2.0f;

		client.body_len = 0;
		client.path.emplace_back(client.head, 0.0f);
		
		j["id"] = client.id;
		j["x"] = client.head.x;
		j["y"] = client.head.y;
	}

	int fd = ctx.client_contexts[id].fd;
	message_sender::send_player_join(fd, j.dump(), ctx);
}

void message_handler::handle_player_left(
	int id,
	const std::string& body,
	ServerContext& ctx)
{
	dbg_tag("SEQUENCE", "[message_handler::handle_player_left]");
	// 다른 클라이언트들에게 해당 클라이언트의 id를 전송.
	// client_fds와 client_contexts에서 해당 데이터를 삭제 (Server::disconnect_client(fd))
	int fd = ctx.client_contexts[id].fd;
	message_sender::send_player_left(fd, body, ctx);
	ctx.server()->disconnect_client(fd);
}

void message_handler::handle_pos(const std::string& body){
	// 클라이언트는 해당 타입의 메시지를 보내지 않음.
}

void message_handler::handle_angle(int id, const std::string& body, ServerContext& ctx){
	dbg_tag("ANGLE", "[message_handler::handle_angle]");
	// {"angle": "string(float)"}
	json j = json::parse(body);
	float angle = j["angle"];
	
	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	ctx.client_contexts[id].angle = angle;
	dbg_tag("ANGLE", "[handle_angle] ctx.client_contexts[id].angle : ", ctx.client_contexts[id].angle);
}

void message_handler::handle_chat(int id, const std::string& body, ServerContext& ctx){
	dbg_tag("SEQUENCE", "[message_handler::handle_chat]");
	// 단순 broad cast 아직 적합성 검사 미실시
	int fd = ctx.client_contexts[id].fd;
	message_sender::send_chat(fd, body, ctx);
}

void message_handler::handle_hit(int id, const std::string& body, ServerContext& ctx){
	dbg_tag("SEQUENCE", "[message_handler::handle_hit]");
	/*
	struct MsgHitPayload {
		int bodySlitherId;   // 충돌 대상의 고유 ID
		float reportedX;       // 충돌 지점
		float reportedY;
	}*/
	json j = json::parse(body);
	int loser_slither_id = id;
	int winner_slither_id = j["winnerSlitherId"];
	float reported_x = j["reportedX"];
	float reported_y = j["reportedY"];

	bool should_kill = false;
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);

		Vector2 reported_point(reported_x, reported_y);
		const auto& head_xy = ctx.client_contexts[loser_slither_id].head;
		Vector2 actual_head_pos(head_xy.x, head_xy.y);

		if (actual_head_pos.distance_to(reported_point) > 3.0f) {
			dbg_tag("HIT", "[handler_hit] 충돌 무효: 충돌지점의 좌표와 실제 head의 좌표 사이의 거리가 멉니다.");
			return;
		}

		constexpr float hit_tolerance = 1.5f;
		ClientContext winner_slither_ctx = ctx.client_contexts[winner_slither_id];
		Vector2 headPos(winner_slither_ctx.head.x, winner_slither_ctx.head.y);
		if (reported_point.distance_to(headPos) <= hit_tolerance) {
			dbg_tag("HIT", "[handler_hit] 충돌 유효: (slither_id)", loser_slither_id, "가 죽었습니다. (slither_id)", winner_slither_id, "에 의해서.");
			should_kill = true;
		}
		else {
			for (const auto& segment : winner_slither_ctx.body_segments) {
				Vector2 segmentPos(segment.x, segment.y);
				if (reported_point.distance_to(segmentPos) <= hit_tolerance) {
					dbg_tag("HIT", "[handler_hit] 충돌 유효: (slither_id)", loser_slither_id, "가 죽었습니다. (slither_id)", winner_slither_id, "에 의해서.");
					should_kill = true;			
					break;
				}
			}
		}		
	}
	
	if(should_kill) slither_manager::kill_slither(loser_slither_id, ctx);
    dbg_tag("HIT", "[handler_hit] 충돌 무효: 충돌 지점과 가까운 body segment가 없습니다.");
}

void message_handler::handle_eat(int id, const std::string& body, ServerContext& ctx){
	dbg_tag("EAT", "[message_handler::handle_eat]");
	/*
	struct MsgEatPayload {
		int food_id;
		float reported_x;
		float reported_y;
	}*/

	json j = json::parse(body);
	int slither_id = id;
	int food_id = j["foodId"];
	float reported_x = j["reportedX"];
	float reported_y = j["reportedY"];

    Vector2 reported_point(reported_x, reported_y);
	Vector2 head_xy;
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		head_xy = ctx.client_contexts[slither_id].head;
	} 

    if (head_xy.distance_to(reported_point) > 3.0f) {
        dbg_tag("EAT", "[handler_eat] 충돌 무효: 충돌지점의 좌표와 실제 head의 좌표 사이의 거리가 멉니다.");
        return;
    }

	const auto& food_contexts = ctx.food_manager.get_all_food_contexts();
	const auto& food_xy = food_contexts.find(food_id)->second;
    Vector2 food_pos(food_xy.x, food_xy.y);
	constexpr float eat_tolerance = 1.5f;

	if (reported_point.distance_to(food_pos) <= eat_tolerance) {
		dbg_tag("EAT", "[handler_eat] 충돌 유효: (slither_id)", slither_id, "가 먹었습니다. (food_id)", food_id, "를.");
		slither_manager::grow(slither_id, ctx);
		//slither_manager::update_body_len(slither_id, ctx);
		ctx.food_manager.del_food(food_id, ctx);
		message_sender::send_slither_length_update(slither_id, +1, ctx);
		return;
	}

	dbg_tag("EAT", "[handler_eat] 충돌 무효: 충돌 지점과 가까운 body segment가 없습니다.");	
}