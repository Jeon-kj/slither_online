#include "collision_manager.hpp"
#include "vector2.hpp"
#include "game_config.hpp"
#include "slither_manager.hpp"
#include "message_sender.hpp"
#include "debug.hpp"

#include <algorithm>

void check_collision_all(ServerContext& ctx){
	for(int i=0; i<ctx.client_fds.size(); i++){
		int slither_id = ctx.fd_to_id[ctx.client_fds[i]];
		if(ctx.client_contexts[slither_id].collision_enabled == false) continue;
		collision_manager::check_collision_headToHead(slither_id, ctx);
		collision_manager::check_collision_headToBody(slither_id, ctx);
		collision_manager::check_collision_headToFood(slither_id, ctx);
	}
}

void check_collision_headToHead(int slither_id, ServerContext& ctx){
	Vector2 head_pos = ctx.client_contexts[slither_id].head;

	// 머리끼리 충돌 체크
	for(int i=0; i<ctx.client_fds.size(); i++){
		int other_id = ctx.fd_to_id[ctx.client_fds[i]];
		if(other_id == slither_id) continue;
		if(ctx.client_contexts[other_id].collision_enabled == false) continue;

		Vector2 other_head_pos = ctx.client_contexts[other_id].head;
		float distance = head_pos.distance_to(other_head_pos);
		if(distance <= GameConfig::HEAD_RADIUS*2){
			
			if(std::find(collision_manager::dead_slither_ids.begin(), collision_manager::dead_slither_ids.end(), slither_id) == collision_manager::dead_slither_ids.end()){
				collision_manager::dead_slither_ids.push_back(slither_id);
			}
			if(std::find(collision_manager::dead_slither_ids.begin(), collision_manager::dead_slither_ids.end(), other_id) == collision_manager::dead_slither_ids.end()){
				collision_manager::dead_slither_ids.push_back(other_id);
			}
			return;
		}
	}
}

void check_collision_headToBody(int slither_id, ServerContext& ctx){
	Vector2 head_pos = ctx.client_contexts[slither_id].head;

	// 머리-몸통 충돌 체크
	for(int i=0; i<ctx.client_fds.size(); i++){
		int other_id = ctx.fd_to_id[ctx.client_fds[i]];
		if(other_id == slither_id) continue;
		if(ctx.client_contexts[other_id].collision_enabled == false) continue;

		for(auto& segment : ctx.client_contexts[other_id].body_segments){
			float distance = head_pos.distance_to(segment);
			if(distance <= GameConfig::HEAD_RADIUS + GameConfig::BODY_RADIUS){
				// 중복 예외 처리
				if(std::find(collision_manager::dead_slither_ids.begin(), collision_manager::dead_slither_ids.end(), slither_id) == collision_manager::dead_slither_ids.end()){
					collision_manager::dead_slither_ids.push_back(slither_id);
				}
				return;
			}
		}
	}
}

void check_collision_headToFood(int slither_id, ServerContext& ctx){
	Vector2 head_pos = ctx.client_contexts[slither_id].head;

	for(auto& [food_id, food_ctx] : ctx.food_manager.get_all_food_contexts()){
		Vector2 food_pos(food_ctx.x, food_ctx.y);
		float distance = head_pos.distance_to(food_pos);
		if(distance <= GameConfig::HEAD_RADIUS + GameConfig::FOOD_RADIUS){
			collision_manager::eat_events.push_back({slither_id, food_id});
			return;
		}
	}
}

void apply_collision_results(ServerContext& ctx){
	for(int dead_id : collision_manager::dead_slither_ids){
		dbg_tag("HIT", "충돌 유효: (slither_id)", dead_id, "가 죽었습니다.");
		slither_manager::kill_slither_locked(dead_id, ctx);
	}

	for(auto& [slither_id, food_id] : collision_manager::eat_events){
		dbg_tag("EAT", "충돌 유효: (slither_id)", slither_id, "가 먹었습니다. (food_id)", food_id, "를.");
		slither_manager::grow(slither_id, ctx);
		ctx.food_manager.del_food(food_id, ctx);
		message_sender::send_slither_length_update(slither_id, +1, ctx);
		continue;
	}
	collision_manager::dead_slither_ids.clear();
	collision_manager::eat_events.clear();
}