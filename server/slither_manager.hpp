#pragma once
#include "server.hpp"

namespace slither_manager {
    void move_client(int fd, float delta_time, float speed, ServerContext& ctx);
    void grow(int fd, ServerContext& ctx);
    void trim(int fd, ServerContext& ctx);
    void update_body_len(int fd, ServerContext& ctx);
    bool check_collision(int fd, ServerContext& ctx); // (향후)
}
