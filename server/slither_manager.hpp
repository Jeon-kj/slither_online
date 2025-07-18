#pragma once
#include "server.hpp"

namespace slither_manager {
    void move_client(int fd, float delta_time, float speed, ServerContext& ctx);
    void grow(int fd, int additional_segments, ServerContext& ctx);
    void trim(int fd, int target_length, ServerContext& ctx);
    bool check_collision(int fd, ServerContext& ctx); // (향후)
}
