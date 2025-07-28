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

	void send_food_spawn(std::unordered_map<int, FoodContext>& new_foods, ServerContext& ctx);

	void send_food_remove(int food_id, ServerContext& ctx);

	void send_slither_length_update(int slither_id, int delta, ServerContext& ctx);
}