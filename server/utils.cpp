#include "utils.hpp"
#include "debug.hpp"

#include <iostream>
#include <fcntl.h>
#include <unistd.h>
#include <arpa/inet.h>
#include <sys/epoll.h>
#include <random>

void utils::error_handling(const std::string& msg){
	dbg_tag("SEQUENCE", "[utils::error_handling]");
	std::cout << "[Error] : " << msg << std::endl;
}

int utils::set_nonblock(int fd){
	dbg_tag("SEQUENCE", "[utils::set_nonblock]");
	int flags = fcntl(fd, F_GETFL, 0);
	return fcntl(fd, F_SETFL, flags | O_NONBLOCK);
}

void utils::modify_epoll_events(int epoll_fd, int client_fd, uint32_t events) {
	dbg_tag("SEQUENCE", "[utils::modify_epoll_events]");
    epoll_event ev{};
    ev.events = events;
    ev.data.fd = client_fd;

    if (epoll_ctl(epoll_fd, EPOLL_CTL_MOD, client_fd, &ev) == -1) {
        throw std::runtime_error("epoll_ctl MOD failed");
    }
}

void utils::send_queue(int fd, int32_t type, const std::string& body, ServerContext& ctx){
	dbg_tag("SEND", "[utils::send_queue] type : ", type, ", body : ", body);
	std::vector<char> packet;

	int32_t type_net = htonl(type);
	int32_t len_net = htonl(body.size());

	packet.insert(packet.end(),
		reinterpret_cast<char*>(&type_net),
		reinterpret_cast<char*>(&type_net) + sizeof(type_net));

	packet.insert(packet.end(),
		reinterpret_cast<char*>(&len_net),
		reinterpret_cast<char*>(&len_net) + sizeof(len_net));

	packet.insert(packet.end(),
		body.begin(),
		body.end());

	int client_id = ctx.fd_to_id[fd];
	ctx.client_contexts[client_id].send_queue.push_back(packet);
	utils::modify_epoll_events(ctx.server()->get_epoll_fd(), fd, EPOLLIN | EPOLLOUT);
}

float utils::random_float(float min, float max) {
	dbg_tag("SEQUENCE", "[utils::random_float]");
    static std::random_device rd;
    static std::mt19937 gen(rd());
    std::uniform_real_distribution<float> dist(min, max);
    return dist(gen);
}