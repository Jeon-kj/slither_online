#pragma once
#include <vector>
#include <string>
#include <mutex>
#include <deque>

struct ClientContext{
	int fd;
	std::vector<char> recv_buffer;
	
	int id;	
	float x;
	float y;
	float angle;
	std::string turn_dir;

	std::deque<std::vector<char>> send_queue;
};