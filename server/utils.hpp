#pragma once
#include "client_session.hpp"
#include "server.hpp"
#include <string>

namespace utils{
	void error_handling(const std::string& msg);
	int set_nonblock(int fd);
	void modify_epoll_events(int epoll_fd, int client_fd, uint32_t events);
	void send_queue(int fd, int32_t type, const std::string& body, ServerContext& ctx);
}