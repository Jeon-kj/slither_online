#include "slither_manager.hpp"
#include "debug.hpp"
#include "json.hpp"
#include "message_sender.hpp"
#include "game_config.hpp"
#include "vector2.hpp"

#include <cmath>

using json = nlohmann::json;

void slither_manager::add_sample(Vector2 new_pos, int id, ServerContext& ctx){
	ClientContext& client = ctx.client_contexts[id];

	if(client.path.size() == 0){
		client.path.emplace_back(new_pos, 0.0f);
		client.head = new_pos;
		return;
	}
	auto last = client.path.back();
	float d = new_pos.distance_to(last.pos);
	if (d <= 0.0) return;

	client.path.emplace_back(new_pos, last.cum_len + d);
	client.head = new_pos;
}

void slither_manager::trim_to_physical_len(float physical_len, int id, ServerContext& ctx){
	ClientContext& client = ctx.client_contexts[id];

	if (client.path.size() < 2) return;
	float head_cum_len = client.path.back().cum_len;
	float keep_from = head_cum_len - physical_len;

	auto it = client.path.begin();
	while (client.path.size() >= 2 && it != client.path.end())
	{
		it++;

		if (it == client.path.end() || it->cum_len >= keep_from) break;

		// 커서가 제거될 노드를 가리키면 먼저 다음으로 올려 붙임
		for (int i = 0; i < client.cursors.size(); i++){
			if (client.cursors[i] == client.path.begin()) {
				client.cursors[i] = it;
			}
		}

		client.path.pop_front();
	}
}

void slither_manager::advance_cursor_to_tarrget(int i, int id, ServerContext& ctx){
	ClientContext& client = ctx.client_contexts[id];
	float head_cum_len = client.path.back().cum_len;

	auto last_node = client.path.back();

	float target_cum_len = head_cum_len - (i + 1) * GameConfig::SPACE;
	if (!client.path.empty())
		target_cum_len = fmax(target_cum_len, client.path.front().cum_len); // 경계 클램프

	auto it = client.cursors[i];
	while (!(*it == last_node) && (*it).cum_len < target_cum_len)
		it++;
	client.cursors[i] = it;

	client.body_segments[i] = (*it).pos;
}

void slither_manager::move_client(int id, float delta_time, float speed, ServerContext& ctx){
	dbg_tag("SLITHER_MOVE", "[slither_manager::move_client]");
	ClientContext& client = ctx.client_contexts[id];

	float dx = std::cos(client.angle) * speed * delta_time;
    float dy = std::sin(client.angle) * speed * delta_time;

	size_t len = client.body_segments.size();
	int blen = std::min((int)len, client.body_len);

	Vector2 new_pos(client.head.x + dx, client.head.y + dy);
	
	slither_manager::add_sample(new_pos, id, ctx);

	float physical_len = GameConfig::SPACE * client.body_len;

	slither_manager::trim_to_physical_len(physical_len, id, ctx);

	int i = 0;
	while (i < client.cursors.size())
	{
		slither_manager::advance_cursor_to_tarrget(i, id, ctx);
		i++;
	}
}

void slither_manager::grow(int id, ServerContext& ctx){
	dbg("[slither_manager::grow]");
	ClientContext& client = ctx.client_contexts[id];

	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	client.body_len++;
	while(client.body_segments.size() < client.body_len){
		int body_segment_size = client.body_segments.size();

		auto node = client.path.begin();
		client.cursors.push_back(node);
		Vector2 pos = node->pos;

		client.body_segments.push_back(pos);
	}
}

void slither_manager::trim(int id, ServerContext& ctx){
	dbg("[slither_manager::trim]");
	ClientContext& client = ctx.client_contexts[id];
	if(client.body_len <= 0) return;

	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	client.body_len--;
	while(client.body_segments.size() > client.body_len){
		client.body_segments.pop_back();
		client.cursors.pop_back();
	}
}

/*void slither_manager::update_body_len(int id, ServerContext &ctx){
	dbg("[slither_manager::update_body_len]");
	std::lock_guard<std::mutex> lock(ctx.server_mutex);
	ctx.client_contexts[id].body_len = ctx.client_contexts[id].body_segments.size();
}*/

void slither_manager::kill_slither(int target_id, ServerContext& ctx){
	dbg("[slither_manager::kill_slither]");
	// Slither 위치에 먹이 생성 + 먹이 생성 전송
	ctx.food_manager.spawn_food_on_death(target_id, ctx);

	int target_fd = ctx.client_contexts[target_id].fd;
	ctx.server()->disconnect_client(target_fd);
	// 사용자들에게 Slither 제거 메시지 전송
	json j;
	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		j["id"]=target_id;
	}

	message_sender::send_player_left(target_fd, j.dump(), ctx);
}