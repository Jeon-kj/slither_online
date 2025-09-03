#pragma once
#include "client_session.hpp"
#include "food_manager.hpp"

#include <unordered_map>
#include <vector>
#include <mutex>

class Server;

struct ServerContext {
public:
    void set_server(Server* server) { server_ = server; }
    Server* server() const { return server_; }

    std::unordered_map<int, ClientContext> client_contexts;	// id -> ClientContext
	std::unordered_map<int, int> fd_to_id;	// fd -> id
    std::vector<int> client_fds;
    std::mutex server_mutex;
    FoodManager food_manager;

private:
    Server* server_ = nullptr;
};

class Server {
public:
	Server(int port, ServerContext& ctx);
	~Server();
	void start();
	void stop();	

	void disconnect_client(int fd);
	void disconnect_client_locked(int fd);
	int get_epoll_fd() const {return epoll_fd_;}

private:
	int port_;
	int server_fd_;
	int epoll_fd_;
	bool running_;

	ServerContext& ctx_;

	void setup_server_socket();
	void setup_epoll();	
	void accept_client();
	void handle_read(int fd);
	void handle_write(int fd);
	void handle_write_locked(int fd);
	void main_loop();
};