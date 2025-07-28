#pragma once
#include "server.hpp"

namespace message_handler {	
	bool extract_message(int fd, int32_t& type, std::string& body, ServerContext& ctx);
	
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
		int fd,
		const std::string& body,
		ServerContext& ctx
	);

	void handle_pos(const std::string& body);

	void handle_angle(int fd, const std::string& body, ServerContext& ctx);

	void handle_chat(int fd, const std::string& body, ServerContext& ctx);

	void handler_hit(int fd, const std::string& body, ServerContext& ctx);
	
	void handler_eat(int fd, const std::string& body, ServerContext& ctx);
}