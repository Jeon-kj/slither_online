#pragma once
#include "client_session.hpp"
#include "server.hpp"
#include <string>
#include <unordered_map>

enum MessageType{
	MSG_PLAYER_JOIN = 1,
	MSG_PLAYER_LEFT = 2,
	MSG_POS = 3,
	MSG_ANGLE = 4,
	MSG_CHAT = 5
};

namespace message {
	bool extract_message(int fd, int32_t type, std::string body, ServerContext& ctx);
	
	void handle_message(
		Server& server,
		int fd, 
		int32_t type, 
		const std::string& body, 
		ServerContext& ctx
	);

	void handle_player_join(
		int fd, 
		const std::string& body, 
		ServerContext& ctx
	);

	void handle_player_left(
		Server& server,
		int fd, 
		ServerContext& ctx
	);

	void handle_pos(const std::string& body);

	void handle_angle(int fd, const std::string& body, ServerContext& ctx);

	void handle_chat(int fd, const std::string& body, ServerContext& ctx);

	void send_player_join(
		int fd, 
		const std::string& body, 
		ServerContext& ctx
	);

	void send_player_left(
		int fd, 
		const std::string& body, 
		ServerContext& ctx
	);

	void send_pos(int fd, ServerContext& ctx);

	void send_chat(int fd, const std::string& body, ServerContext& ctx);
}