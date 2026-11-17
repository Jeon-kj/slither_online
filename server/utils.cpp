#include "utils.hpp"
#include "debug.hpp"
#include "game_config.hpp"

#include <iostream>
#include <fcntl.h>
#include <unistd.h>
#include <arpa/inet.h>
#include <sys/epoll.h>
#include <random>
#include <cstring>

void utils::error_handling(const std::string& msg){
	dbg_tag("SEQUENCE", "[utils::error_handling]");
	std::cout << "[Error] : " << msg << std::endl;
}

int utils::set_nonblock(int fd){
	dbg_tag("SEQUENCE", "[utils::set_nonblock]");
	int flags = fcntl(fd, F_GETFL, 0);
	return fcntl(fd, F_SETFL, flags | O_NONBLOCK);
}

// PRECONDITION: caller must hold ctx_.server_mutex.
void utils::erase_from_vector(std::vector<int>& v, int fd) {
    v.erase(std::remove(v.begin(), v.end(), fd), v.end());
}

bool utils::fd_valid(int fd){
    return fd >= 0 && !(fcntl(fd, F_GETFD) == -1 && errno == EBADF);
}

// PRECONDITION: caller must hold ctx.server_mutex.
bool utils::modify_epoll_events(int epoll_fd, ClientContext& client, uint32_t events, Server& server) {
	dbg_tag("SEQUENCE", "[utils::modify_epoll_events]");
	// 닫힘/무효 FD 가드(EBADF 방지)
	if (client.closing || client.fd < 0 || !utils::fd_valid(client.fd)) return false;

    epoll_event ev{};
    ev.events = events;
    ev.data.fd = client.fd;

	if (!client.epoll_registered) {
        if (epoll_ctl(epoll_fd, EPOLL_CTL_ADD, client.fd, &ev) == 0) {
            client.epoll_registered = true;
			client.current_events = events;
            return true;
        } else {
            int e = errno;
            dbg_tag("EPOLL", "ADD fail fd=", client.fd," errno=", e," msg=", std::strerror(e));
            client.closing = true;
            server.disconnect_client_locked(client.fd);
            return false;
        }
    }

	if (client.current_events == events) return true; // 불필요한 MOD 방지

	if (epoll_ctl(epoll_fd, EPOLL_CTL_MOD, client.fd, &ev) == 0) {
        client.current_events = events;
        return true;
    }

	int e = errno;
    if (e == ENOENT) {
        // 등록이 풀렸다면 ADD로 복구 시도
        if (epoll_ctl(epoll_fd, EPOLL_CTL_ADD, client.fd, &ev) == 0) {
            client.epoll_registered = true;
            client.current_events = events;
            dbg_tag("EPOLL", "MOD->ADD recovered fd=", client.fd);
            return true;
        } else {
            // 복구 실패
            dbg_tag("EPOLL", "ADD recover fail fd=", client.fd, " errno=", errno, " msg=", std::strerror(errno));
            client.closing = true; 
            server.disconnect_client_locked(client.fd);
            return false;
        }
    } else {
        // EINVAL, EPERM, EBADF 등
        dbg_tag("EPOLL", "MOD fail fd=", client.fd, " errno=", e, " msg=", std::strerror(e));
        client.closing = true;
        server.disconnect_client_locked(client.fd);
        return false;
    }
}

// PRECONDITION: caller must hold ctx.server_mutex.
void utils::send_queue(int fd, int32_t type, const std::string& body, ServerContext& ctx){
	dbg_tag("SEND", "[utils::send_queue] type : ", type, ", body : ", body);
	auto it = ctx.fd_to_id.find(fd);
	if (it == ctx.fd_to_id.end()) return;
	auto itc = ctx.client_contexts.find(it->second);
	if (itc == ctx.client_contexts.end()) return;
	ClientContext& client = itc->second;
	if (client.closing || client.send_queue_overflow || client.fd < 0) return;

	constexpr std::size_t header_size = sizeof(int32_t) * 2;
	const std::size_t remaining = GameConfig::MAX_SEND_QUEUE_BYTES - client.send_queue_bytes;
	if (remaining < header_size || body.size() > remaining - header_size) {
		// 순회 종료 후 연결 정리 예약
		client.send_queue_overflow = true;
		std::cout << "Send queue limit exceeded: " << fd << std::endl;
		return;
	}

	std::vector<char> packet;
	packet.reserve(header_size + body.size());

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

	const std::size_t packet_size = packet.size();
	client.send_queue.push_back(std::move(packet));
	client.send_queue_bytes += packet_size;
	utils::modify_epoll_events(ctx.server()->get_epoll_fd(), client, EPOLLIN | EPOLLOUT, *ctx.server());
}

float utils::random_float(float min, float max) {
	dbg_tag("SEQUENCE", "[utils::random_float]");
    static std::random_device rd;
    static std::mt19937 gen(rd());
    std::uniform_real_distribution<float> dist(min, max);
    return dist(gen);
}