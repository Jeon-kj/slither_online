#pragma once
#include "vector2.hpp"

#include <vector>
#include <string>
#include <mutex>
#include <deque>
#include <list>

struct SlitherPath{
	Vector2 pos;
	float cum_len;

	SlitherPath(Vector2 pos_, float cum_len_) : pos(pos_), cum_len(cum_len_) {}

	bool operator==(const SlitherPath& other) const {
        return pos == other.pos && cum_len == other.cum_len;
    }
};

struct ClientContext{
	int fd=-1;
	int id=-1;

	// fd관리
	bool epoll_registered = false;   // ADD 성공 시 true
    bool closing = false;            // disconnect 진행 중
    uint32_t current_events = 0;     // epoll에 실제 등록된 이벤트

	// 변수 초기화 완료 여부 / join 이후 활성화
	bool activation_flag = false; 
	
	// 송수신
	std::vector<char> recv_buffer;
	std::deque<std::vector<char>> send_queue;

	// object 관리
	Vector2 head;
	std::deque<Vector2> body_segments;
	std::list<SlitherPath> path;
	std::vector<std::list<SlitherPath>::iterator> cursors;
	int body_len;

	// 이동
	bool boost_status = false;
	float boost_timer = 0.0f;
	float angle;
	std::string turn_dir;	

	ClientContext() = default;
	ClientContext(int fd_, int id_) : fd(fd_), id(id_) {}
};