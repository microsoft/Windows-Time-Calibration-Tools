// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#pragma once

#if defined(_MSC_VER)
#include "targetver.h"

#include <Ws2tcpip.h>
#include <winsock.h>
#include <tchar.h>

#else
#include <sys/types.h>
#include <sys/socket.h>
#include <arpa/inet.h>
#include <netdb.h>

#define SOCKET int
#define INVALID_SOCKET -1
#define SOCKET_ERROR -1

#endif

#include <stdio.h>


// TODO: reference additional headers your program requires here
