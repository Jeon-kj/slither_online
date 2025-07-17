#include "server.hpp"

int main(){
	int port = 12345;
	Server server(port);
	server.start();
	
	return 0;
}