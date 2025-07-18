#include "message_handler.hpp"
#include "debug.hpp"
#include "json.hpp"
#include "message_type.hpp"
#include "message_sender.hpp"

#include <arpa/inet.h>

using json = nlohmann::json;

bool message_handler::extract_message(int fd, int32_t& type, std::string& body, ServerContext& ctx){
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
void message_handler::handle_message(
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
			message_handler::handle_player_join(fd, body, ctx);
			break;
		
		case MSG_PLAYER_LEFT:
			dbg("[handle_message] MSG_PLAYER_LEFT");
			message_handler::handle_player_left(server, fd, ctx);
			break;

		case MSG_POS:
			dbg("[handle_message] MSG_POS");
			message_handler::handle_pos(body);
			break;

		case MSG_ANGLE:
			dbg("[handle_message] MSG_ANGLE");
			message_handler::handle_angle(fd, body, ctx);
			break;

		case MSG_CHAT:
			dbg("[handle_message] MSG_CHAT");
			message_handler::handle_chat(fd, body, ctx);
			break;
	}
}

void message_handler::handle_player_join(
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
	
	message_sender::send_player_join(fd, j.dump(), ctx);
}

void message_handler::handle_player_left(
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
	
	message_sender::send_player_left(fd, j.dump(), ctx);
	dbg("disconnect_client (handle_player_left)");
	server.disconnect_client(fd);
}

void message_handler::handle_pos(const std::string& body){
	// 클라이언트는 해당 타입의 메시지를 보내지 않음.
}

void message_handler::handle_angle(int fd, const std::string& body, ServerContext& ctx){
	// {"angle": "string(float)"}
	dbg("[handle_angle]");
	json j = json::parse(body);
	float angle = std::stof(body);
	
	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	ctx.client_contexts[fd].angle = angle;
	dbg("[handle_angle] ctx.client_contexts[fd].angle : ", ctx.client_contexts[fd].angle);
}

void message_handler::handle_chat(int fd, const std::string& body, ServerContext& ctx){
	// 단순 broad cast 아직 적합성 검사 미실시
	message_sender::send_chat(fd, body, ctx);
}