#include "server.hpp"
#include <vector>

namespace collision_manager {
	void check_collision_all(ServerContext& ctx);
	// 머리끼리 충돌 결과 확인
	void check_collision_headToHead(int slither_id, ServerContext& ctx);
	void check_collision_headToBody(int slither_id, ServerContext& ctx);
	void check_collision_headToFood(int slither_id, ServerContext& ctx);

	void apply_collision_results(ServerContext& ctx);

	// 필요한 정보
	// - 사망 예정 플레이어 ID 집합
	std::vector<int> dead_slither_ids;
	// - 먹이를 획득할 플레이어와 먹이 ID
	std::vector<std::pair<int, int>> eat_events;
}