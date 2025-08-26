#pragma once
#include "server.hpp"

namespace message_handler {	
	bool extract_message(int id, int32_t& type, std::string& body, ServerContext& ctx);
	
	void handle_message(
		Server& server,
		int id, 
		int32_t type, 
		const std::string& body, 
		ServerContext& ctx
	);

	void handle_player_join(
		int id, 
		const std::string& body, 
		ServerContext& ctx
	);

	void handle_player_left(
		int id,
		const std::string& body,
		ServerContext& ctx
	);

	void handle_pos(const std::string& body);

	void handle_angle(int id, const std::string& body, ServerContext& ctx);

	void handle_chat(int id, const std::string& body, ServerContext& ctx);

	void handle_hit(int id, const std::string& body, ServerContext& ctx);
	
	void handle_eat(int id, const std::string& body, ServerContext& ctx);

	void handle_boost_status(int id, const std::string& body, ServerContext& ctx);
}