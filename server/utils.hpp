#pragma once
#include "client_session.hpp"
#include "server.hpp"

#include <string>
#include <algorithm>

namespace utils{
	void error_handling(const std::string& msg);
	int set_nonblock(int fd);
	void erase_from_vector(std::vector<int>& v, int fd);
	bool fd_valid(int fd);
	bool modify_epoll_events(int epoll_fd, ClientContext& client, uint32_t events, Server& server);
	void del_and_close_locked(int epfd, ClientContext& c);
	void send_queue(int fd, int32_t type, const std::string& body, ServerContext& ctx);
	float random_float(float min = 0.0f, float max = 1.0f);
}