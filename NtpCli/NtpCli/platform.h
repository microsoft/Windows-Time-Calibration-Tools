#pragma once

inline int MyGetLastError()
{
#if defined(_MSC_VER)
    return WSAGetLastError();
#else
    return errno;
#endif
}

#if defined(_MSC_VER)
inline void PlatformInit()
{
    WORD wVersionRequested;
    WSADATA wsaData;
    wVersionRequested = MAKEWORD(2, 0);
    DWORD err = WSAStartup(wVersionRequested, &wsaData);
    if (err != 0)
    {
        printf("WSAStartup failed %d\n", err);
        exit(err);
    }
}
#else
inline void PlatformInit()
{
    return;
}

#endif
