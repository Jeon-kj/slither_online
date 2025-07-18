#include "utils.hpp"
#include <iostream>
#include <fcntl.h>
#include <unistd.h>
#include <cmath>
#include <arpa/inet.h>
#include <sys/epoll.h>

void utils::error_handling(const std::string& msg){
	std::cout << "[Error] : " << msg << std::endl;
}

int utils::set_nonblock(int fd){
	int flags = fcntl(fd, F_GETFL, 0);
	return fcntl(fd, F_SETFL, flags | O_NONBLOCK);
}

void utils::modify_epoll_events(int epoll_fd, int client_fd, uint32_t events) {
    epoll_event ev{};
    ev.events = events;
    ev.data.fd = client_fd;

    if (epoll_ctl(epoll_fd, EPOLL_CTL_MOD, client_fd, &ev) == -1) {
        throw std::runtime_error("epoll_ctl MOD failed");
    }
}

void utils::send_queue(int fd, int32_t type, const std::string& body, ServerContext& ctx){
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

	{
		std::lock_guard<std::mutex> lock(ctx.server_mutex);
		ctx.client_contexts[fd].send_queue.push_back(packet);
		utils::modify_epoll_events(ctx.epoll_fd, fd, EPOLLIN | EPOLLOUT);
	}
}

void utils::move_client(int fd, float delta_time, float speed, ServerContext& ctx){
	ClientContext& client = ctx.client_contexts[fd];

	float dx = std::cos(client.angle) * speed * delta_time;
    float dy = std::sin(client.angle) * speed * delta_time;

	client.head.x += dx;
    client.head.y += dy;
}