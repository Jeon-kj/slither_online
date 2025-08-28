#include "food_manager.hpp"
#include "server.hpp"
#include "utils.hpp"
#include "game_config.hpp"
#include "message_sender.hpp"
#include "debug.hpp"

#include <random>

void FoodManager::spawn_food_random(ServerContext& ctx){
	dbg_tag("SEQUENCE", "[FoodManager::spawn_food_random]");
	std::unordered_map<int, FoodContext>& food_contexts = ctx.food_manager.get_all_food_contexts();

	std::unordered_map<int, FoodContext> new_foods;
	while(food_contexts.size()<GameConfig::INIT_FOOD_COUNT){
		dbg_tag("FOOD", "[FoodManager::spawn_food_random] food_contexts.size() : ", food_contexts.size(), ", GameConfig::INIT_FOOD_COUNT : ", GameConfig::INIT_FOOD_COUNT);
		float x = utils::random_float(-GameConfig::MAP_WIDTH/2, GameConfig::MAP_WIDTH/2);
		float y = utils::random_float(-GameConfig::MAP_HEIGHT/2, GameConfig::MAP_HEIGHT/2);

		int usable_id = pop_usable_id(ctx);
		FoodContext food(usable_id,x, y);		
		dbg_tag("FOOD", "usable_id : ", usable_id);
		new_foods[usable_id] = food;
		food_contexts[usable_id] = food;
	}
	message_sender::send_food_spawn(new_foods, ctx);
}

void FoodManager::spawn_food_on_death(int dead_id, ServerContext& ctx){
	dbg_tag("SEQUENCE", "[FoodManager::spawn_food_on_death] dead_id : ", dead_id);
	std::unordered_map<int, FoodContext>& food_contexts = ctx.food_manager.get_all_food_contexts();
	ClientContext& dead_client = ctx.client_contexts[dead_id];

	std::unordered_map<int, FoodContext> new_foods;
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		while(new_foods.size() < dead_client.body_len+1){
			float x;
			float y;
			if(new_foods.size() == 0){
				x = dead_client.head.x;
				y = dead_client.head.y;
			}
			else{
				Vector2 dead_body = dead_client.body_segments.back();
				dead_client.body_segments.pop_back();
				x = dead_body.x;
				y = dead_body.y;
			}
			
			int usable_id = pop_usable_id(ctx);
			FoodContext food(usable_id, x, y);			
			new_foods[usable_id] = food;
			food_contexts[usable_id] = food;
		}
	}

	message_sender::send_food_spawn(new_foods, ctx);
}

void FoodManager::spawn_food_on_death_locked(int dead_id, ServerContext& ctx){
	dbg_tag("SEQUENCE", "[FoodManager::spawn_food_on_death_locked] dead_id : ", dead_id);
	std::unordered_map<int, FoodContext>& food_contexts = ctx.food_manager.get_all_food_contexts();
	ClientContext& dead_client = ctx.client_contexts[dead_id];

	std::unordered_map<int, FoodContext> new_foods;
	while(new_foods.size() < dead_client.body_len+1){
		float x;
		float y;
		if(new_foods.size() == 0){
			x = dead_client.head.x;
			y = dead_client.head.y;
		}
		else{
			Vector2 dead_body = dead_client.body_segments.back();
			dead_client.body_segments.pop_back();
			x = dead_body.x;
			y = dead_body.y;
		}
		
		int usable_id = pop_usable_id(ctx);
		FoodContext food(usable_id, x, y);			
		new_foods[usable_id] = food;
		food_contexts[usable_id] = food;
	}

	message_sender::send_food_spawn(new_foods, ctx);
}

void FoodManager::del_food(int food_id, ServerContext &ctx){
	dbg_tag("SEQUENCE", "[FoodManager::del_food] food_id : ", food_id);
	std::unordered_map<int, FoodContext>& food_contexts = ctx.food_manager.get_all_food_contexts();
	food_contexts.erase(food_id);
	push_usable_id(food_id, ctx);

	message_sender::send_food_remove(food_id, ctx);
}

int FoodManager::pop_usable_id(ServerContext &ctx){
	dbg_tag("SEQUENCE", "[FoodManager::pop_usable_id]");
	if(ctx.food_manager.empty_queue()) return ctx.food_manager.get_contexts_size();

	std::queue<int>& reusable_ids = ctx.food_manager.get_all_reusable_ids();
	int food_id = reusable_ids.front();
	reusable_ids.pop();
	return food_id;
}

void FoodManager::push_usable_id(int food_id, ServerContext &ctx){
	dbg_tag("SEQUENCE", "[FoodManager::push_usable_id]");
	std::queue<int>& reusable_ids = ctx.food_manager.get_all_reusable_ids();
	reusable_ids.push(food_id);
}