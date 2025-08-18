#pragma once
#include "server.hpp"

namespace slither_manager {
    void add_sample(Vector2 new_pos, int id, ServerContext& ctx);
    void trim_to_physical_len(float physical_len, int id, ServerContext& ctx);
    void advance_cursor_to_tarrget(int i, int id, ServerContext& ctx);
    
    void move_client(int id, float delta_time, float speed, ServerContext& ctx);
    void grow(int id, ServerContext& ctx);
    void trim(int id, ServerContext& ctx);
    void update_body_len(int id, ServerContext& ctx);
    void kill_slither(int target_id, ServerContext& ctx);
}
