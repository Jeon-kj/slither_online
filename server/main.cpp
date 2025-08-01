#include "server.hpp"
#include "debug.hpp"

int main(){
	dbg_tag("SEQUENCE", "[main]");
	int port = 12345;
	ServerContext ctx;
	Server server(port, ctx);
	ctx.set_server(&server);
	server.start();
	
	return 0;
}