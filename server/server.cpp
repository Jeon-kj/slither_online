#include "server.hpp"
#include "utils.hpp"
#include "message_handler.hpp"
#include "message_sender.hpp"
#include "slither_manager.hpp"
#include "debug.hpp"
#include "game_config.hpp"
#include "food_manager.hpp"
#include "collision_manager.hpp"
#include "json.hpp"

#include <iostream>
#include <unistd.h>
#include <sys/socket.h>
#include <sys/epoll.h>
#include <arpa/inet.h>
#include <algorithm>
#include <thread>
#include <cstring>

Server::Server(int port, ServerContext& ctx)
	: port_(port), ctx_(ctx)
{
	server_fd_ = -1;
	epoll_fd_ = -1;
	running_ = false;
}

Server::~Server(){
	stop();
	if(server_fd_ != -1) close(server_fd_);
	if(epoll_fd_ != -1) close(epoll_fd_);
}

void Server::start(){
	dbg_tag("SEQUENCE", "[Server::start]");
	setup_server_socket();
	setup_epoll();
	running_ = true;
	main_loop();
}

void Server::stop(){
	dbg_tag("SEQUENCE", "[Server::stop]");
	running_ = false;
}

void Server::setup_server_socket(){
	dbg_tag("SEQUENCE", "[Server::setup_server_socket]");
	server_fd_ = socket(AF_INET, SOCK_STREAM, 0);

	// SO_REUSEADDR 옵션 설정
	int opt = 1;
	if (setsockopt(server_fd_, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt)) < 0) {
		utils::error_handling("setsockopt(SO_REUSEADDR) error");
	}
	
	sockaddr_in server_addr{};
	server_addr.sin_family = AF_INET;
	server_addr.sin_port = htons(port_);
	server_addr.sin_addr.s_addr = INADDR_ANY;

	if(bind(server_fd_, (sockaddr*)&server_addr, sizeof(server_addr)) == -1)
		utils::error_handling("bind error");
	if(listen(server_fd_, SOMAXCONN))
		utils::error_handling("listen error");

	utils::set_nonblock(server_fd_);
}

void Server::setup_epoll(){
	dbg_tag("SEQUENCE", "[Server::setup_epoll]");
	epoll_fd_ = epoll_create1(0);
	epoll_event ev{};
	ev.events = EPOLLIN;
	ev.data.fd = server_fd_;
	epoll_ctl(epoll_fd_, EPOLL_CTL_ADD, server_fd_, &ev);
	
	std::cout << "epoll server listening on port " << port_ << std::endl;
}

void Server::accept_client(){
	dbg_tag("SEQUENCE", "[Server::accept_client]");

	sockaddr_in client_addr{};
	socklen_t addr_len = sizeof(client_addr);

	int client_fd = accept(server_fd_, (sockaddr*)&client_addr, &addr_len);
	if(client_fd == -1) {
		utils::error_handling("accept error");
		return;
	}
	
	utils::set_nonblock(client_fd);

	{
		std::lock_guard<std::mutex> lock(ctx_.server_mutex);
		ClientContext c{};
		c.fd = client_fd;
		c.id = client_fd;
		c.epoll_registered = false;
		c.current_events = 0;
		c.closing = false;

		ctx_.client_contexts.emplace(c.fd, c);
		ctx_.fd_to_id[client_fd] = client_fd;
		ctx_.client_fds.push_back(client_fd);

		ClientContext& client = ctx_.client_contexts[client_fd];

		if (!utils::modify_epoll_events(epoll_fd_, client, EPOLLIN, *this)) {
			return;
		}

	}

	std::cout << "Client connected: " << client_fd << std::endl;
}

void Server::handle_read(int fd){
	dbg_tag("SEQUENCE", "[Server::handle_read]");
	char buf[1024];
	while(true)	{
		ssize_t n = recv(fd, buf, sizeof(buf), 0);
		if(n > 0){
			{
				std::lock_guard<std::mutex> lock(ctx_.server_mutex);

				auto it = ctx_.fd_to_id.find(fd);
                if (it == ctx_.fd_to_id.end()) return;
				int client_id = it->second;

				ClientContext& client = ctx_.client_contexts[client_id];

				if (client.closing || client.fd < 0) return;
				
				
				client.recv_buffer.insert(
					client.recv_buffer.end(), 
					buf, 
					buf+n
				);
			}

			int32_t type;
			std::string body;
			while(true) {
				message_handler::ExtractResult result;
				{
					std::lock_guard<std::mutex> lock(ctx_.server_mutex);
					auto it = ctx_.fd_to_id.find(fd);
                    if (it == ctx_.fd_to_id.end()) break;
					int client_id = it->second;

					result = message_handler::extract_message(client_id, type, body, ctx_);
					if (result == message_handler::ExtractResult::InvalidMessage) {
						disconnect_client_locked(fd);
						return;
					}
				}

				if (result == message_handler::ExtractResult::NeedMoreData) break;

				message_handler::handle_message(*this, fd, type, body, ctx_);
			}
		}
		else if(n == 0){
			disconnect_client(fd);
			break;
		}
		else {
			if(errno == EAGAIN || errno == EWOULDBLOCK) break;

			disconnect_client(fd);
			break;
		}
	}
}

void Server::handle_write(int fd){
	dbg_tag("SEQUENCE", "[Server::handle_write]");
	{
		std::lock_guard<std::mutex> lock(ctx_.server_mutex);
		handle_write_locked(fd);
	}
}

void Server::handle_write_locked(int fd){
	dbg_tag("SEQUENCE", "[Server::handle_write_locked]");
	auto it = ctx_.fd_to_id.find(fd);
	if (it == ctx_.fd_to_id.end()) return;
	ClientContext& client = ctx_.client_contexts[it->second];

	if (client.closing || client.fd < 0) return;

	while (!client.send_queue.empty()) {
		auto& msg = client.send_queue.front();
		ssize_t sent = send(fd, msg.data(), msg.size(), MSG_NOSIGNAL);
		if (sent < 0) {
			if (errno == EAGAIN || errno == EWOULDBLOCK) {
				// 버퍼 꽉 찼음. 다음 EPOLLOUT을 기다림
				break;
			} else {
				// 진짜 에러 발생: 연결 종료 처리
				dbg("disconnect_client (handle_write)");
				disconnect_client_locked(fd);
				return;
			}
		}

		if (sent < (ssize_t)msg.size()) {
			// 아직 덜 보낸 데이터가 남았음
			// 덜 보낸 부분으로 msg를 잘라야 함
			msg.erase(msg.begin(), msg.begin() + sent);
			break;
		}

		client.send_queue.pop_front();
	}

	if (client.send_queue.empty()) {
		utils::modify_epoll_events(epoll_fd_, client, EPOLLIN, *this);
	}
}

void Server::main_loop(){
	dbg_tag("SEQUENCE", "[Server::main_loop]");
	epoll_event events[GameConfig::MAX_EVENTS];
	auto network_loop = [&]() {
		while(running_) {
			int nfds = epoll_wait(epoll_fd_, events, GameConfig::MAX_EVENTS, -1);
			if (nfds < 0) {
				if (errno == EINTR) continue; // 시그널 깨짐(정삭적 중단)
				dbg_tag("EPOLL","epoll_wait fail errno=", errno, " ", std::strerror(errno));
				break; // 혹은 안전한 종료 경로로
			}
			for(int i=0; i<nfds; ++i){
				int fd = events[i].data.fd;
				uint32_t ev = events[i].events;

				if(fd == server_fd_){
					// 소켓 accept 처리
					accept_client();
				}
				
				if (ev & (EPOLLERR | EPOLLHUP | EPOLLRDHUP)) {
					disconnect_client(fd);
					continue;
				}

				if(ev & EPOLLIN){
					// 클라이언트 소켓 송수신 처리
					handle_read(fd);
				}

				// EPOLLOUT 처리
				if (ev & EPOLLOUT) {
					std::lock_guard<std::mutex> lock(ctx_.server_mutex);
            		if (ctx_.fd_to_id.count(fd)) handle_write_locked(fd);
				}				
			}
		}
	};

	auto game_loop = [&](){
		using Clock = std::chrono::steady_clock;
		const auto tick_interval = std::chrono::duration_cast<Clock::duration>(
			std::chrono::duration<double>(GameConfig::DELTA_TIME));
		auto next_tick = Clock::now();
		while(running_){
			{
				dbg_tag("LOCK", "[game_loop] server_mutex_ 시도");	
				std::lock_guard<std::mutex> lock(ctx_.server_mutex);
				dbg_tag("LOCK","[game_loop] server_mutex_ 완료");

				std::vector<int> pending_kill_ids;

				// slither move logic
				if(!ctx_.client_fds.empty()){
					for(auto client_fd: ctx_.client_fds){
						auto it = ctx_.fd_to_id.find(client_fd);
						if (it == ctx_.fd_to_id.end()) return;
						int client_id = it->second;

						auto itc = ctx_.client_contexts.find(client_id);
						if (itc == ctx_.client_contexts.end()) return;
						ClientContext& client = itc->second;

						if(!client.activation_flag) continue;
						
						client.spawn_time += GameConfig::DELTA_TIME;
						if(client.spawn_time >= GameConfig::SPAWN_IMMUNITY_TIME){
							client.collision_enabled = true;
						}

						if(client.boost_status && client.body_len > 0){
							slither_manager::move_client(client_id, GameConfig::DELTA_TIME, GameConfig::BOOST_SPEED, pending_kill_ids, ctx_);
							client.boost_timer += GameConfig::DELTA_TIME;
							if(client.boost_timer >= GameConfig::BOOST_TICK){
								std::vector<Vector2> trimmed_positions;
								client.boost_timer = 0;
								// body len 줄이는 로직
								slither_manager::trim(client_id, trimmed_positions, ctx_);
								ctx_.food_manager.spawn_food_on_boost(trimmed_positions, ctx_);
								message_sender::send_slither_length_update_locked(client_id, -1, ctx_);
							}
						}							
						else
							slither_manager::move_client(client_id, GameConfig::DELTA_TIME, GameConfig::BASIC_SPEED, pending_kill_ids, ctx_);						
					}

					for(auto dead_id: pending_kill_ids){
						slither_manager::kill_slither_locked(dead_id, ctx_);
					}
					
					message_sender::send_pos(ctx_);
				}
				else{
					dbg_tag("SLITHER_MOVE", "[game_loop] 대기 중 - 클라이언트 없음");
				}

				// collision check logic
				if(GameConfig::ENABLE_COLLISION){
					collision_manager::check_collision_all(ctx_);
					collision_manager::apply_collision_results(ctx_);
				}				

				// food spawn logic
				ctx_.food_manager.spawn_food_random(ctx_);
			}			
			// 처리 시간을 포함한 고정 틱 간격
			next_tick += tick_interval;
			const auto now = Clock::now();
			// 처리 초과 시 밀린 틱의 연속 실행 방지
			if (next_tick < now) next_tick = now;
			std::this_thread::sleep_until(next_tick);
		}
	};

	std::thread net_thread(network_loop);
	std::thread game_thread(game_loop);

	game_thread.join();
	net_thread.join();	
}

// PRECONDITION: caller must NOT hold ctx.server_mutex.
void Server::disconnect_client(int fd){
	dbg_tag("SEQUENCE", "[Server::disconnect_client]");
	std::lock_guard<std::mutex> lock(ctx_.server_mutex);

	disconnect_client_locked(fd);
}

// PRECONDITION: caller must hold ctx.server_mutex.
void Server::disconnect_client_locked(int fd){
	dbg_tag("SEQUENCE", "[Server::disconnect_client]");

	auto it = ctx_.fd_to_id.find(fd);
    if (it == ctx_.fd_to_id.end()) return;

	int client_id = it->second;

	auto itc = ctx_.client_contexts.find(client_id);
    if (itc == ctx_.client_contexts.end()) {
        ctx_.fd_to_id.erase(fd);
        utils::erase_from_vector(ctx_.client_fds, fd);
        return;
    }

	ClientContext& client = itc->second;

	if (client.closing || client.fd < 0) {
        // 이미 정리 중/정리됨
        ctx_.fd_to_id.erase(fd);
        utils::erase_from_vector(ctx_.client_fds, fd);
        ctx_.client_contexts.erase(itc);
        return;
    }

	bool was_active = client.activation_flag;

	client.closing = true;	// 먼저 닫힘 표시
    int cfd = client.fd;	// 로컬로 보관(아래에서 fd=-1로 바꿀 예정)

	if (cfd >= 0) {
		// epoll에서 제거(실패 무시: 이미 사라졌을 수 있음)
        epoll_ctl(epoll_fd_, EPOLL_CTL_DEL, cfd, nullptr);

        ::close(cfd);
    }

    // 상태 플래그 표준화
    client.fd = -1;
    client.epoll_registered = false;
    client.current_events = 0;

    // 컨테이너/매핑 정리
    ctx_.fd_to_id.erase(fd);
    utils::erase_from_vector(ctx_.client_fds, fd);
    ctx_.client_contexts.erase(itc);

    if (was_active) {
        nlohmann::json body = {{"id", client_id}};
        message_sender::send_player_left_locked(cfd, body.dump(), ctx_);
    }

    std::cout << "Client disconnected: " << cfd << std::endl;
}
