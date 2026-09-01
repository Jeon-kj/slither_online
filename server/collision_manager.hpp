#include "server.hpp"

namespace collision_manager {
	void check_collision_all(ServerContext& ctx);
	// 머리끼리 충돌 결과 확인
	void check_collision_headToHead(int slither_id, ServerContext& ctx);
	void check_collision_headToBody(int slither_id, ServerContext& ctx);
	void check_collision_headToFood(int slither_id, ServerContext& ctx);

	void apply_collision_results(ServerContext& ctx);
}