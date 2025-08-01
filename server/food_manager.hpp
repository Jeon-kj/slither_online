#pragma once
#include <unordered_map>
#include <queue>

// 전방선언(server.hpp가 해당 hpp를 include 하여서 server.hpp를 include 할 수 없음)
struct ServerContext;

struct FoodContext{
	int id; 
	
	float x;
	float y;

	FoodContext(float x_ = 0, float y_ = 0) : x(x_), y(y_) {}
};

class FoodManager{
private:
	std::unordered_map<int, FoodContext> food_contexts_;
	std::queue<int> reusable_ids_;
public:
	void spawn_food_random(ServerContext& ctx);
	void spawn_food_on_death(int dead_id, ServerContext& ctx);
	void del_food(int food_id, ServerContext& ctx);

	std::unordered_map<int, FoodContext>& get_all_food_contexts() { return food_contexts_;}
	std::queue<int>& get_all_reusable_ids() { return reusable_ids_;}
	bool empty_queue() {return reusable_ids_.empty(); }
	int get_contexts_size() { return food_contexts_.size(); }

	int pop_usable_id(ServerContext &ctx);
	void push_usable_id(int food_id, ServerContext &ctx);
	bool check_collision(ServerContext& ctx); // (향후)
};