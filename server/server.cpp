#include "server.hpp"
#include "utils.hpp"
#include "message_handler.hpp"
#include "message_sender.hpp"
#include "slither_manager.hpp"
#include "debug.hpp"

#include <iostream>
#include <unistd.h>
#include <sys/socket.h>
#include <sys/epoll.h>
#include <arpa/inet.h>
#include <algorithm>
#include <thread>

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

	epoll_event ev{};
	sockaddr_in client_addr{};
	socklen_t addr_len = sizeof(client_addr);

	int client_fd = accept(server_fd_, (sockaddr*)&client_addr, &addr_len);
	if(client_fd == -1) utils::error_handling("accept error");
	
	utils::set_nonblock(client_fd);

	ev.events = EPOLLIN;
	ev.data.fd = client_fd;

	epoll_ctl(epoll_fd_, EPOLL_CTL_ADD, client_fd, &ev);

	{
		std::lock_guard<std::mutex> lock(ctx_.server_mutex);
		ctx_.client_contexts.emplace(
			client_fd,
			ClientContext{client_fd, client_fd}
		);
		ctx_.client_fds.push_back(client_fd);
		ctx_.fd_to_id[client_fd] = client_fd;
	}

	std::cout << "Client connected: " << client_fd << std::endl;
}

void Server::handle_read(int fd){
	dbg_tag("SEQUENCE", "[Server::handle_read]");
	char buf[1024];
	while(true)	{
		ssize_t n = recv(fd, buf, sizeof(buf), 0);
		if(n > 0){
			dbg_tag("SEQUENCE", "[handle_read] n > 0");
			{
				//std::lock_guard<std::mutex> lock(ctx_.server_mutex);
				int client_id = ctx_.fd_to_id[fd];
				ctx_.client_contexts[client_id].recv_buffer.insert(
					ctx_.client_contexts[client_id].recv_buffer.end(), 
					buf, 
					buf+n
				);
			}

			int32_t type;
			std::string body;
			while(true) {
				bool loop_check;
				{
					//std::lock_guard<std::mutex> lock(ctx_.server_mutex);
					int client_id = ctx_.fd_to_id[fd];
					loop_check = message_handler::extract_message(client_id, type, body, ctx_);
				}

				if(!loop_check) break;

				message_handler::handle_message(*this, fd, type, body, ctx_);
			}
		}
		else if(n == 0){
			dbg_tag("SEQUENCE", "[handle_read] n == 0");
			disconnect_client(fd);
			break;
		}
		else {
			dbg_tag("SEQUENCE", "[handle_read] else, n : ", n);
			if(errno == EAGAIN || errno == EWOULDBLOCK) break;

			disconnect_client(fd);
			break;
		}
	}
}

void Server::handle_write(int fd){
	dbg_tag("SEQUENCE", "[Server::handle_write]");
	bool should_disconnect = false;

	{
		std::lock_guard<std::mutex> lock(ctx_.server_mutex);
		int client_id = ctx_.fd_to_id[fd];
		ClientContext& client = ctx_.client_contexts[client_id];	

		while (!client.send_queue.empty()) {
			auto& msg = client.send_queue.front();
			ssize_t sent = send(fd, msg.data(), msg.size(), 0);
			if (sent < 0) {
				if (errno == EAGAIN || errno == EWOULDBLOCK) {
					// 버퍼 꽉 찼음. 다음 EPOLLOUT을 기다림
					break;
				} else {
					// 진짜 에러 발생: 연결 종료 처리
					dbg("disconnect_client (handle_write)");
					should_disconnect = true;
					break;
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

		if (client.send_queue.empty() && !should_disconnect) {
			utils::modify_epoll_events(epoll_fd_, fd, EPOLLIN);
		}
	}
	
	if(should_disconnect) disconnect_client(fd);
}

void Server::main_loop(){
	dbg_tag("SEQUENCE", "[Server::main_loop]");
	constexpr int MAX_EVENTS = 10;

	epoll_event events[MAX_EVENTS];
	auto network_loop = [&]() {
		while(running_) {
			int nfds = epoll_wait(epoll_fd_, events, MAX_EVENTS, -1);
			for(int i=0; i<nfds; ++i){
				int fd = events[i].data.fd;
				if(fd == server_fd_){
					// 소켓 accept 처리
					accept_client();
				}					
				else{
					if(events[i].events & EPOLLIN){
						// 클라이언트 소켓 송수신 처리
						handle_read(fd);
					}
					// EPOLLOUT 처리
					if (events[i].events & EPOLLOUT) {
						handle_write(fd);
					}
				}					
			}
		}
	};

	auto game_loop = [&](){
		while(running_){
			{
				dbg_tag("LOCK", "[game_loop] server_mutex_ 시도");	
				std::lock_guard<std::mutex> lock(ctx_.server_mutex);
				dbg_tag("LOCK","[game_loop] server_mutex_ 완료");

				// slither move logic
				if(!ctx_.client_fds.empty()){
					for(auto client_fd: ctx_.client_fds){
						dbg_tag("SLITHER_MOVE", "[game_loop] client_fd:", client_fd);
						int client_id = ctx_.fd_to_id[client_fd];
						slither_manager::move_client(client_id, 0.02f, 1.0f, ctx_);
						message_sender::send_pos(client_fd, ctx_);
					}				
				}
				else{
					dbg_tag("SLITHER_MOVE", "[game_loop] 대기 중 - 클라이언트 없음");
				}

				// food spawn logic
				ctx_.food_manager.spawn_food_random(ctx_);
			}			
			std::this_thread::sleep_for(std::chrono::milliseconds(20));
		}
	};

	std::thread net_thread(network_loop);
	std::thread game_thread(game_loop);

	game_thread.join();
	net_thread.join();	
}

void Server::disconnect_client(int fd){
	dbg_tag("SEQUENCE", "[Server::disconnect_client]");
	std::lock_guard<std::mutex> lock(ctx_.server_mutex);
	epoll_ctl(epoll_fd_, EPOLL_CTL_DEL, fd, nullptr);
	close(fd);
	int client_id = ctx_.fd_to_id[fd];
	ctx_.client_contexts.erase(client_id);
	ctx_.fd_to_id.erase(fd);
	ctx_.client_fds.erase(
            std::remove(ctx_.client_fds.begin(), ctx_.client_fds.end(), fd),
            ctx_.client_fds.end()
        );

	std::cout << "Client disconnected: " << fd << std::endl;
}