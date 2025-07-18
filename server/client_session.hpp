#pragma once
#include <vector>
#include <string>
#include <mutex>
#include <deque>

struct SlitherSegment{
	float x;
	float y;
};

struct ClientContext{
	int fd;
	std::vector<char> recv_buffer;
	
	int id;	
	float angle;
	std::string turn_dir;

	// 추후에 충돌 또는 득점으로 인한 길이 증가와 감소 로직 추가 예정
	// 현재는 body_segments가 서버에서 사용되지 않음
	SlitherSegment head;
	std::deque<SlitherSegment> body_segments;

	std::deque<std::vector<char>> send_queue;
};