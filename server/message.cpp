#include "message.hpp"
#include "json.hpp"
#include "client_session.hpp"
#include "utils.hpp"

#include <cstring>
#include <arpa/inet.h>
#include "debug.hpp"

using json = nlohmann::json;

bool message::extract_message(int fd, int32_t& type, std::string& body, ServerContext& ctx){
	dbg("[extract_message]");
	ClientContext& client = ctx.client_contexts[fd];
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
void message::handle_message(
	Server& server,
	int fd, 
	int32_t type, 
	const std::string& body, 
	ServerContext& ctx)
{
	switch (type)
	{
		case MSG_PLAYER_JOIN:
			dbg("[handle_message] MSG_PLAYER_JOIN");
			message::handle_player_join(fd, body, ctx);
			break;
		
		case MSG_PLAYER_LEFT:
			dbg("[handle_message] MSG_PLAYER_LEFT");
			message::handle_player_left(server, fd, ctx);
			break;

		case MSG_POS:
			dbg("[handle_message] MSG_POS");
			message::handle_pos(body);
			break;

		case MSG_ANGLE:
			dbg("[handle_message] MSG_ANGLE");
			message::handle_angle(fd, body, ctx);
			break;

		case MSG_CHAT:
			dbg("[handle_message] MSG_CHAT");
			message::handle_chat(fd, body, ctx);
			break;
	}
}

void message::handle_player_join(
	int fd, 
	const std::string& body, 
	ServerContext& ctx)
{
	dbg("[handle_player_join]");
	// body 안에는 아무것도 없음 (0byte)
	// 정보를 설정해주고 id를 붙여서 다른 플레이어들에게 전송.
	// 본인 정보와 다른 모든 플레이어들의 정보를 join한 플레이어에게 전송.

	json j;
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		ClientContext& client = ctx.client_contexts[fd];
		client.id = client.fd;
		client.head.x = rand() % 10 - 5;
		client.head.y = rand() % 10 - 5;
		client.turn_dir = "w";
		client.angle = M_PI / 2.0f;
		
		j["id"] = client.id;
		j["x"] = client.head.x;
		j["y"] = client.head.y;
	}
	
	message::send_player_join(fd, j.dump(), ctx);
}

void message::handle_player_left(
	Server& server, 
	int fd, 
	ServerContext& ctx)
{
	// 클라이언트가 해당 타입의 메시지를 보낼지 모르겠음.
	// 타입의 데이터만 옴.
	// 다른 클라이언트들에게 해당 클라이언트의 id를 전송.
	// client_fds와 client_contexts에서 해당 데이터를 삭제 (Server::disconnect_client(fd))
	json j;
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		j["id"]=ctx.client_contexts[fd].id;
	}
	
	message::send_player_left(fd, j.dump(), ctx);
	dbg("disconnect_client (handle_player_left)");
	server.disconnect_client(fd);
}

void message::handle_pos(const std::string& body){
	// 클라이언트는 해당 타입의 메시지를 보내지 않음.
}

void message::handle_angle(int fd, const std::string& body, ServerContext& ctx){
	// {"angle": "string(float)"}
	dbg("[handle_angle]");
	json j = json::parse(body);
	float angle = std::stof(body);
	
	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	ctx.client_contexts[fd].angle = angle;
	dbg("[handle_angle] ctx.client_contexts[fd].angle : ", ctx.client_contexts[fd].angle);
}

void message::handle_chat(int fd, const std::string& body, ServerContext& ctx){
	// 단순 broad cast 아직 적합성 검사 미실시
	message::send_chat(fd, body, ctx);
}

void message::send_player_join(
	int fd, 
	const std::string& body, 
	ServerContext& ctx)
{
	dbg("[send_player_join]");
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		for(auto client_fd : ctx.client_fds){
			dbg("[send_player_join] client_fd : ", client_fd);
			// fd를 제외한 모든 클라이언트들에게 전송 (얘 들어왔어요~)
			if(client_fd == fd) continue;
			utils::send_queue(client_fd, MSG_PLAYER_JOIN, body, ctx);
		}
	}
	
	json j = json::array();
	
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		for(const auto& [fd, client] : ctx.client_contexts){
			dbg("[send_player_join] client_id : ", client.id);
			j.push_back({
				{"id", client.id},
				{"x", client.head.x},
				{"y", client.head.y}
			});
		}
	}

	json wrapper;
	wrapper["id"] = ctx.client_contexts[fd].id;
	wrapper["payloads"] = j;

	std::string send_body = wrapper.dump();

	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		utils::send_queue(fd, MSG_PLAYER_JOIN, send_body, ctx);
	}
}

void message::send_player_left(int fd, const std::string& body, ServerContext& ctx){
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		for(auto client_fd : ctx.client_fds){
			if(client_fd == fd) continue;
			utils::send_queue(client_fd, MSG_PLAYER_LEFT, body, ctx);
		}
	}
}

void message::send_pos(int fd, ServerContext& ctx){
	dbg("[send_pos]");
	// 지금 현재의 위치를 전송. (즉, 움직임 처리 함수 호출 이후에 호출해야함.)
	json j;
	{
		//std::lock_guard<std::mutex> lock(ctx.server_mutex);
		ClientContext& client = ctx.client_contexts[fd];
	
		j["id"] = client.id;
		j["x"] = client.head.x;
		j["y"] = client.head.y;
		j["length"] = client.body_segments.size();
	}

	std::string body = j.dump();
	dbg("[send_pos] body : ", body);
	{
		//std::lock_guard<std::mutex> lock(ctx.server_mutex);
		utils::send_queue(fd, MSG_POS, body, ctx);
	}	
}

void message::send_chat(int fd, const std::string& body, ServerContext& ctx){
	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	for(auto client_fd : ctx.client_fds){
		if(client_fd == fd) continue;
		utils::send_queue(client_fd, MSG_CHAT, body, ctx);
	}
}