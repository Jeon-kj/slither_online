#include "slither_manager.hpp"
#include "debug.hpp"

#include <cmath>

void slither_manager::move_client(int fd, float delta_time, float speed, ServerContext& ctx){
	dbg("[move_client]");
	ClientContext& client = ctx.client_contexts[fd];

	float dx = std::cos(client.angle) * speed * delta_time;
    float dy = std::sin(client.angle) * speed * delta_time;

	// client.body_len 갱신 로직 추가
	// client.body_segments, body_len 비교하여 감소 증가 로직 필요
	size_t len = client.body_segments.size();
	int blen = std::min((int)len, client.body_len);

	for(int i=blen-1; i>0; i--){
		client.body_segments[i] = client.body_segments[i-1];
	}
	if(!client.body_segments.empty()){
		client.body_segments[0].x = client.head.x;
		client.body_segments[0].y = client.head.y;
	}

	client.head.x += dx;
    client.head.y += dy;
}

void slither_manager::grow(int fd, ServerContext& sCtx){
	ClientContext& cCtx = sCtx.client_contexts[fd];

	while(cCtx.body_segments.size() < cCtx.body_len){
		cCtx.body_segments.emplace_back(cCtx.head.x, cCtx.head.y);
	}
}

void slither_manager::trim(int fd, ServerContext& sCtx){
	ClientContext& cCtx = sCtx.client_contexts[fd];

	while(cCtx.body_segments.size() > cCtx.body_len){
		cCtx.body_segments.pop_back();
	}
}

void slither_manager::update_body_len(int fd, ServerContext &ctx){
	ctx.client_contexts[fd].body_len = ctx.client_contexts[fd].body_segments.size();
}