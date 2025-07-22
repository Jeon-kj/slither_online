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

void message_sender::send_player_left(int fd, const std::string& body, ServerContext& ctx){
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		for(auto client_fd : ctx.client_fds){
			if(client_fd == fd) continue;
			utils::send_queue(client_fd, MSG_PLAYER_LEFT, body, ctx);
		}
	}
}

void message_sender::send_pos(int fd, ServerContext& ctx){
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

void message_sender::send_chat(int fd, const std::string& body, ServerContext& ctx){
	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	for(auto client_fd : ctx.client_fds){
		if(client_fd == fd) continue;
		utils::send_queue(client_fd, MSG_CHAT, body, ctx);
	}
}

void message_sender::send_food_spawn(std::unordered_map<int, FoodContext>& new_foods, ServerContext& ctx){
	json j = json::array();;
	for(const auto& [id, food] : new_foods){
		j.push_back({
			{"id", food.id},
			{"x", food.x},
			{"y", food.y}
		});
	}

	json wrapper;
	wrapper["payloads"] = j;

	std::string send_body = wrapper.dump();

	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	for(int fd : ctx.client_fds)
	{		
		utils::send_queue(fd, MSG_FOOD_SPAWN, send_body, ctx);
	}
}

void message_sender::send_food_remove(int food_id, ServerContext& ctx){
	json j;
	j["id"] = food_id;
	std::string send_body = j.dump();
	
	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	for(int fd : ctx.client_fds)
	{		
		utils::send_queue(fd, MSG_FOOD_SPAWN, send_body, ctx);
	}	
}