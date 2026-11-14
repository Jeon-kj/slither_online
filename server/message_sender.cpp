#include "message_sender.hpp"
#include "debug.hpp"
#include "json.hpp"
#include "utils.hpp"
#include "message_type.hpp"

using json = nlohmann::json;

void message_sender::send_player_join(
	int fd, 
	const std::string& body, 
	ServerContext& ctx)
{
	dbg_tag("SEQUENCE", "[message_sender::send_player_join]");
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		for(auto client_fd : ctx.client_fds){
			dbg_tag("JOIN", "[message_sender::send_player_join2] client_fd : ", client_fd);
			// fd를 제외한 모든 클라이언트들에게 전송 (얘 들어왔어요~)
			if(client_fd == fd) continue;
			utils::send_queue(client_fd, MSG_PLAYER_JOIN, body, ctx);
		}
	}
	
	json slithers = json::array();
	json foods = json::array();
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		for(const auto& [id, client] : ctx.client_contexts){
			dbg_tag("JOIN", "[message_sender::send_player_join3] client_id : ", client.id);
			slithers.push_back({
				{"id", client.id},
				{"x", client.head.x},
				{"y", client.head.y},
				{"length", client.body_len}
			});
		}

		for(const auto& [id, food] : ctx.food_manager.get_all_food_contexts()){
			dbg_tag("JOIN", "[message_sender::send_player_join4] food_id : ", food.id);
			foods.push_back({
				{"id", food.id},
				{"x", food.x},
				{"y", food.y}
			});
		}
	}

	json wrapper;
	wrapper["id"] = ctx.fd_to_id[fd];
	wrapper["slitherPayloads"] = slithers;
	wrapper["foodPayloads"] = foods;

	std::string send_body = wrapper.dump();

	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		utils::send_queue(fd, MSG_PLAYER_JOIN, send_body, ctx);
		ctx.client_contexts[fd].activation_flag = true;
	}
}

// PRECONDITION: caller must NOT hold ctx.server_mutex.
void message_sender::send_player_left(int fd, const std::string& body, ServerContext& ctx){
	dbg_tag("SEQUENCE", "[message_sender::send_player_left]");
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		for(auto client_fd : ctx.client_fds){
			if(client_fd == fd) continue;
			utils::send_queue(client_fd, MSG_PLAYER_LEFT, body, ctx);
		}
	}
}
// PRECONDITION: caller must hold ctx.server_mutex.
void message_sender::send_player_left_locked(int fd, const std::string& body, ServerContext& ctx){
	dbg_tag("SEQUENCE", "[message_sender::send_player_left]");
	for(auto client_fd : ctx.client_fds){
		if(client_fd == fd) continue;
		utils::send_queue(client_fd, MSG_PLAYER_LEFT, body, ctx);
	}
}

void message_sender::send_pos(ServerContext& ctx){
	dbg_tag("SEQUENCE", "[message_sender::send_pos]");
	// 지금 현재의 위치를 전송. (즉, 움직임 처리 함수 호출 이후에 호출해야함.)

	json positions = json::array();
	std::vector<int> recipients;
	{
		// 호출부의 서버 락 유지
		for(auto client_fd : ctx.client_fds){
			int client_id = ctx.fd_to_id[client_fd];
			ClientContext& client = ctx.client_contexts[client_id];
			if (client.activation_flag) recipients.push_back(client_fd);

			positions.push_back({
				{"id", client.id},
				{"x", client.head.x},
				{"y", client.head.y},
				{"angle", client.angle}
			});
		}		
	}

	if (recipients.empty()) return;

	json wrapper;
	wrapper["posPayloads"] = positions;
	std::string body = wrapper.dump();
	// 전체 수신자에게 동일한 위치 JSON 재사용
	for (int fd : recipients) {
		utils::send_queue(fd, MSG_POS, body, ctx);
	}	
}

void message_sender::send_chat(int fd, const std::string& body, ServerContext& ctx){
	dbg_tag("SEQUENCE", "[message_sender::send_chat]");
	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	for(auto client_fd : ctx.client_fds){
		utils::send_queue(client_fd, MSG_CHAT, body, ctx);
	}
}

// PRECONDITION: caller must hold ctx.server_mutex.
void message_sender::send_food_spawn(std::unordered_map<int, FoodContext>& new_foods, ServerContext& ctx){
	dbg_tag("SEQUENCE", "[message_sender::send_food_spawn]");
	if(new_foods.empty()) return;

	json j = json::array();;
	for(const auto& [id, food] : new_foods){
		dbg_tag("FOOD", "[message_sender::send_food_spawn] id : ", food.id, ", x : ", food.x, ", y : ", food.y);
		j.push_back({
			{"id", food.id},
			{"x", food.x},
			{"y", food.y}
		});
	}

	json wrapper;
	wrapper["payloads"] = j;

	std::string send_body = wrapper.dump();

	//std::lock_guard<std::mutex> lock(ctx.server_mutex);
	for(int fd : ctx.client_fds)
	{		
		utils::send_queue(fd, MSG_FOOD_SPAWN, send_body, ctx);
	}
}

// PRECONDITION: caller must NOT hold ctx.server_mutex.
void message_sender::send_food_remove(int food_id, ServerContext& ctx){
	dbg_tag("FOOD_REMOVE", "[message_sender::send_food_remove]");
	json j;
	j["id"] = food_id;
	std::string send_body = j.dump();
	
	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	for(int fd : ctx.client_fds)
	{		
		utils::send_queue(fd, MSG_FOOD_REMOVE, send_body, ctx);
	}	
}

// PRECONDITION: caller must hold ctx.server_mutex.
void message_sender::send_food_remove_locked(int food_id, ServerContext& ctx){
	dbg_tag("FOOD_REMOVE", "[message_sender::send_food_remove_locked]");
	json j;
	j["id"] = food_id;
	std::string send_body = j.dump();
	
	for(int fd : ctx.client_fds)
	{		
		utils::send_queue(fd, MSG_FOOD_REMOVE, send_body, ctx);
	}	
}

// PRECONDITION: caller must NOT hold ctx.server_mutex.
void message_sender::send_slither_length_update(int slither_id, int delta, ServerContext& ctx){
	// delta = -1/+1
	dbg_tag("SEQUENCE", "[message_sender::send_slither_length_update] slither_id : ", slither_id, ", delta : ", delta);

	json j;
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		j["id"] = slither_id;
		j["length"] = ctx.client_contexts[slither_id].body_len;
		j["delta"] = delta;
	}
	
	std::string send_body = j.dump();

	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	for(int fd : ctx.client_fds)
	{		
		utils::send_queue(fd, MSG_SLITHER_LENGTH_UPDATE, send_body, ctx);
	}	
}

// PRECONDITION: caller must hold ctx.server_mutex.
void message_sender::send_slither_length_update_locked(int slither_id, int delta, ServerContext& ctx){
	// delta = -1/+1
	dbg_tag("SEQUENCE", "[message_sender::send_slither_length_update] slither_id : ", slither_id, ", delta : ", delta);

	json j;
	j["id"] = slither_id;
	j["length"] = ctx.client_contexts[slither_id].body_len;
	j["delta"] = delta;
	std::string send_body = j.dump();
	
	for(int fd : ctx.client_fds)
	{		
		utils::send_queue(fd, MSG_SLITHER_LENGTH_UPDATE, send_body, ctx);
	}	
}