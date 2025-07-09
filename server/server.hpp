#pragma once
#include "client_session.hpp"

#include <unordered_map>
#include <vector>
#include <mutex>

struct ServerContext {
    std::unordered_map<int, ClientContext>& client_contexts;
    std::vector<int>& client_fds;
    std::mutex& server_mutex;
	int& epoll_fd;
};

class Server {
public:
	Server(int port);
	~Server();
	void start();
	void stop();	

	void disconnect_client(int fd);

private:
	int port_;
	int server_fd_;
	int epoll_fd_;
	bool running_;
	std::unordered_map<int, ClientContext> client_contexts_;
	std::vector<int> client_fds_;
	std::mutex server_mutex_;
	ServerContext ctx_ {
		client_contexts_,
		client_fds_,
		server_mutex_,
		epoll_fd_	
	};

	void setup_server_socket();
	void setup_epoll();
	void accept_client();
	void handle_read(int fd);
	void handle_write(int fd);
	void main_loop();
};