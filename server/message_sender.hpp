#include "server.hpp"

namespace message_sender {
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