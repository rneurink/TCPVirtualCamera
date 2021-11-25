#pragma warning(disable:4244)
#pragma warning(disable:4711)

#undef UNICODE

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include <streams.h>
#include <stdio.h>
#include <olectl.h>
#include <dvdmedia.h>
#include "filters.h"

#include <winsock2.h>
#include <ws2tcpip.h>
#include <wspiapi.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <sstream>

// Need to link with Ws2_32.lib
#pragma comment (lib, "Ws2_32.lib")
// #pragma comment (lib, "Mswsock.lib")

#define DEFAULT_PORT 1100

DWORD WINAPI ServerThread(LPVOID lpParam);
bool CheckIfPortAvailable(short int dwPort);

#define BUFFER_LENGTH (BMP_WIDTH * BMP_HEIGHT * 3)

SOCKET server_socket = INVALID_SOCKET;
long bufferlength;
byte* framePointer;
byte frameBuffer[BUFFER_LENGTH];


#pragma region CVCam
//////////////////////////////////////////////////////////////////////////
//  CVCam is the source filter which masquerades as a capture device
//////////////////////////////////////////////////////////////////////////
CUnknown * WINAPI CVCam::CreateInstance(LPUNKNOWN lpunk, HRESULT *phr)
{
    ASSERT(phr);
    CUnknown *punk = new CVCam(lpunk, phr);
    return punk;
}

CVCam::CVCam(LPUNKNOWN lpunk, HRESULT *phr) : 
    CSource(NAME("Virtual Cam"), lpunk, CLSID_VirtualCam)
{
    ASSERT(phr);
    CAutoLock cAutoLock(&m_cStateLock);
    // Create the one and only output pin
    m_paStreams = (CSourceStream **) new CVCamStream*[1];
    m_paStreams[0] = new CVCamStream(phr, this, L"Virtual TCP Cam");
}

HRESULT CVCam::QueryInterface(REFIID riid, void **ppv)
{
    //Forward request for IAMStreamConfig & IKsPropertySet to the pin
    if(riid == _uuidof(IAMStreamConfig) || riid == _uuidof(IKsPropertySet))
        return m_paStreams[0]->QueryInterface(riid, ppv);
    else
        return CSource::QueryInterface(riid, ppv);
}

//////////////////////////////////////////////////////////////////////////
// CVCamStream is the one and only output pin of CVCam which handles 
// all the stuff.
//////////////////////////////////////////////////////////////////////////
CVCamStream::CVCamStream(HRESULT *phr, CVCam *pParent, LPCWSTR pPinName) :
    CSourceStream(NAME("Virtual TCP Cam"),phr, pParent, pPinName), m_pParent(pParent)
{
    // Set the default media type as 320x240x24@15
    GetMediaType(4, &m_mt);
    SetupServer();
}

CVCamStream::~CVCamStream()
{
    CleanupServer();
} 

HRESULT CVCamStream::QueryInterface(REFIID riid, void **ppv)
{   
    // Standard OLE stuff
    if(riid == _uuidof(IAMStreamConfig))
        *ppv = (IAMStreamConfig*)this;
    else if(riid == _uuidof(IKsPropertySet))
        *ppv = (IKsPropertySet*)this;
    else
        return CSourceStream::QueryInterface(riid, ppv);

    AddRef();
    return S_OK;
}


//////////////////////////////////////////////////////////////////////////
//  This is the routine where we create the data being output by the Virtual
//  Camera device.
//////////////////////////////////////////////////////////////////////////

HRESULT CVCamStream::FillBuffer(IMediaSample *pms)
{
    REFERENCE_TIME rtNow;
    
    REFERENCE_TIME avgFrameTime = ((VIDEOINFOHEADER*)m_mt.pbFormat)->AvgTimePerFrame;

    rtNow = m_rtLastTime;
    m_rtLastTime += avgFrameTime;
    pms->SetTime(&rtNow, &m_rtLastTime);
    pms->SetSyncPoint(TRUE);

    BYTE *pData;
    long lDataLen;
    pms->GetPointer(&pData);
    lDataLen = pms->GetSize();

    memcpy_s(pData, lDataLen, framePointer, BMP_HEIGHT * BMP_WIDTH * 3);

    return NOERROR;
} // FillBuffer


//
// Notify
// Ignore quality management messages sent from the downstream filter
STDMETHODIMP CVCamStream::Notify(IBaseFilter * pSender, Quality q)
{
    return E_NOTIMPL;
} // Notify

//////////////////////////////////////////////////////////////////////////
// This is called when the output format has been negotiated
//////////////////////////////////////////////////////////////////////////
HRESULT CVCamStream::SetMediaType(const CMediaType *pmt)
{
    DECLARE_PTR(VIDEOINFOHEADER, pvi, pmt->Format());
    HRESULT hr = CSourceStream::SetMediaType(pmt);
    return hr;
}

// See Directshow help topic for IAMStreamConfig for details on this method
HRESULT CVCamStream::GetMediaType(int iPosition, CMediaType *pmt)
{
    if(iPosition < 0) return E_INVALIDARG;
    if(iPosition > 8) return VFW_S_NO_MORE_ITEMS;

    if(iPosition == 0) 
    {
        *pmt = m_mt;
        return S_OK;
    }

    DECLARE_PTR(VIDEOINFOHEADER, pvi, pmt->AllocFormatBuffer(sizeof(VIDEOINFOHEADER)));
    ZeroMemory(pvi, sizeof(VIDEOINFOHEADER));

    pvi->bmiHeader.biCompression = BI_RGB;
    pvi->bmiHeader.biBitCount    = 24;
    pvi->bmiHeader.biSize       = sizeof(BITMAPINFOHEADER);
    pvi->bmiHeader.biWidth      = BMP_WIDTH;
    pvi->bmiHeader.biHeight     = BMP_HEIGHT;
    pvi->bmiHeader.biPlanes     = 1;
    pvi->bmiHeader.biSizeImage  = GetBitmapSize(&pvi->bmiHeader);
    pvi->bmiHeader.biClrImportant = 0;

    pvi->AvgTimePerFrame = 20000;

    SetRectEmpty(&(pvi->rcSource)); // we want the whole image area rendered.
    SetRectEmpty(&(pvi->rcTarget)); // no particular destination rectangle

    pmt->SetType(&MEDIATYPE_Video);
    pmt->SetFormatType(&FORMAT_VideoInfo);
    pmt->SetTemporalCompression(FALSE);

    // Work out the GUID for the subtype from the header info.
    const GUID SubTypeGUID = GetBitmapSubtype(&pvi->bmiHeader);
    pmt->SetSubtype(&SubTypeGUID);
    pmt->SetSampleSize(pvi->bmiHeader.biSizeImage);
    
    return NOERROR;

} // GetMediaType

// This method is called to see if a given output format is supported
HRESULT CVCamStream::CheckMediaType(const CMediaType *pMediaType)
{
    VIDEOINFOHEADER *pvi = (VIDEOINFOHEADER *)(pMediaType->Format());
    if(*pMediaType != m_mt) 
        return E_INVALIDARG;
    return S_OK;
} // CheckMediaType

// This method is called after the pins are connected to allocate buffers to stream data
HRESULT CVCamStream::DecideBufferSize(IMemAllocator *pAlloc, ALLOCATOR_PROPERTIES *pProperties)
{
    CAutoLock cAutoLock(m_pFilter->pStateLock());
    HRESULT hr = NOERROR;

    VIDEOINFOHEADER *pvi = (VIDEOINFOHEADER *) m_mt.Format();
    pProperties->cBuffers = 1;
    pProperties->cbBuffer = pvi->bmiHeader.biSizeImage;

    ALLOCATOR_PROPERTIES Actual;
    hr = pAlloc->SetProperties(pProperties,&Actual);

    if(FAILED(hr)) return hr;
    if(Actual.cbBuffer < pProperties->cbBuffer) return E_FAIL;

    return NOERROR;
} // DecideBufferSize

// Called when graph is run
HRESULT CVCamStream::OnThreadCreate()
{
    m_rtLastTime = 0;
    return NOERROR;
} // OnThreadCreate


//////////////////////////////////////////////////////////////////////////
//  IAMStreamConfig
//////////////////////////////////////////////////////////////////////////

HRESULT STDMETHODCALLTYPE CVCamStream::SetFormat(AM_MEDIA_TYPE *pmt)
{
    DECLARE_PTR(VIDEOINFOHEADER, pvi, m_mt.pbFormat);
    m_mt = *pmt;
    IPin* pin; 
    ConnectedTo(&pin);
    if(pin)
    {
        IFilterGraph *pGraph = m_pParent->GetGraph();
        pGraph->Reconnect(this);
    }
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CVCamStream::GetFormat(AM_MEDIA_TYPE **ppmt)
{
    *ppmt = CreateMediaType(&m_mt);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CVCamStream::GetNumberOfCapabilities(int *piCount, int *piSize)
{
    *piCount = 8;
    *piSize = sizeof(VIDEO_STREAM_CONFIG_CAPS);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CVCamStream::GetStreamCaps(int iIndex, AM_MEDIA_TYPE **pmt, BYTE *pSCC)
{
    *pmt = CreateMediaType(&m_mt);
    DECLARE_PTR(VIDEOINFOHEADER, pvi, (*pmt)->pbFormat);

    if (iIndex == 0) iIndex = 4;

    pvi->bmiHeader.biCompression = BI_RGB;
    pvi->bmiHeader.biBitCount    = 24;
    pvi->bmiHeader.biSize       = sizeof(BITMAPINFOHEADER);
    pvi->bmiHeader.biWidth      = BMP_WIDTH;
    pvi->bmiHeader.biHeight     = BMP_HEIGHT;
    pvi->bmiHeader.biPlanes     = 1;
    pvi->bmiHeader.biSizeImage  = GetBitmapSize(&pvi->bmiHeader);
    pvi->bmiHeader.biClrImportant = 0;

    SetRectEmpty(&(pvi->rcSource)); // we want the whole image area rendered.
    SetRectEmpty(&(pvi->rcTarget)); // no particular destination rectangle

    (*pmt)->majortype = MEDIATYPE_Video;
    (*pmt)->subtype = MEDIASUBTYPE_RGB24;
    (*pmt)->formattype = FORMAT_VideoInfo;
    (*pmt)->bTemporalCompression = FALSE;
    (*pmt)->bFixedSizeSamples= FALSE;
    (*pmt)->lSampleSize = pvi->bmiHeader.biSizeImage;
    (*pmt)->cbFormat = sizeof(VIDEOINFOHEADER);
    
    DECLARE_PTR(VIDEO_STREAM_CONFIG_CAPS, pvscc, pSCC);
    
    pvscc->guid = FORMAT_VideoInfo;
    pvscc->VideoStandard = AnalogVideo_None;
    pvscc->InputSize.cx = 640;
    pvscc->InputSize.cy = 480;
    pvscc->MinCroppingSize.cx = 80;
    pvscc->MinCroppingSize.cy = 60;
    pvscc->MaxCroppingSize.cx = 1920;
    pvscc->MaxCroppingSize.cy = 1080;
    pvscc->CropGranularityX = 80;
    pvscc->CropGranularityY = 60;
    pvscc->CropAlignX = 0;
    pvscc->CropAlignY = 0;

    pvscc->MinOutputSize.cx = 80;
    pvscc->MinOutputSize.cy = 60;
    pvscc->MaxOutputSize.cx = 1920;
    pvscc->MaxOutputSize.cy = 1080;
    pvscc->OutputGranularityX = 0;
    pvscc->OutputGranularityY = 0;
    pvscc->StretchTapsX = 0;
    pvscc->StretchTapsY = 0;
    pvscc->ShrinkTapsX = 0;
    pvscc->ShrinkTapsY = 0;
    pvscc->MinFrameInterval = 200000;   //50 fps
    pvscc->MaxFrameInterval = 50000000; // 0.2 fps
    pvscc->MinBitsPerSecond = (80 * 60 * 3 * 8) / 5;
    pvscc->MaxBitsPerSecond = 640 * 480 * 3 * 8 * 50;

    return S_OK;
}

//////////////////////////////////////////////////////////////////////////
// IKsPropertySet
//////////////////////////////////////////////////////////////////////////


HRESULT CVCamStream::Set(REFGUID guidPropSet, DWORD dwID, void *pInstanceData, 
                        DWORD cbInstanceData, void *pPropData, DWORD cbPropData)
{// Set: Cannot set any properties.
    return E_NOTIMPL;
}

// Get: Return the pin category (our only property). 
HRESULT CVCamStream::Get(
    REFGUID guidPropSet,   // Which property set.
    DWORD dwPropID,        // Which property in that set.
    void *pInstanceData,   // Instance data (ignore).
    DWORD cbInstanceData,  // Size of the instance data (ignore).
    void *pPropData,       // Buffer to receive the property data.
    DWORD cbPropData,      // Size of the buffer.
    DWORD *pcbReturned     // Return the size of the property.
)
{
    if (guidPropSet != AMPROPSETID_Pin)             return E_PROP_SET_UNSUPPORTED;
    if (dwPropID != AMPROPERTY_PIN_CATEGORY)        return E_PROP_ID_UNSUPPORTED;
    if (pPropData == NULL && pcbReturned == NULL)   return E_POINTER;
    
    if (pcbReturned) *pcbReturned = sizeof(GUID);
    if (pPropData == NULL)          return S_OK; // Caller just wants to know the size. 
    if (cbPropData < sizeof(GUID))  return E_UNEXPECTED;// The buffer is too small.
        
    *(GUID *)pPropData = PIN_CATEGORY_CAPTURE;
    return S_OK;
}

// QuerySupported: Query whether the pin supports the specified property.
HRESULT CVCamStream::QuerySupported(REFGUID guidPropSet, DWORD dwPropID, DWORD *pTypeSupport)
{
    if (guidPropSet != AMPROPSETID_Pin) return E_PROP_SET_UNSUPPORTED;
    if (dwPropID != AMPROPERTY_PIN_CATEGORY) return E_PROP_ID_UNSUPPORTED;
    // We support getting this property, but not setting it.
    if (pTypeSupport) *pTypeSupport = KSPROPERTY_SUPPORT_GET; 
    return S_OK;
}

#pragma endregion


//////////////////////////////////////////////////////////////////////////
// 
// 
// 
// TCP Server
// 
// 
// 
//////////////////////////////////////////////////////////////////////////
#pragma region TCP Server

int CVCamStream::SetupServer() {

    bufferlength = sizeof(frameBuffer);
    framePointer = &frameBuffer[0];


    WSADATA wsaData;
    int iResult;

    server_socket = INVALID_SOCKET;

    struct addrinfo hints;

    // Initialize Winsock
    iResult = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (iResult != 0) {
        printf("WSAStartup failed with error: %d\n", iResult);
        return 1;
    }

    ZeroMemory(&hints, sizeof(hints));
    hints.ai_family = AF_INET;
    hints.ai_socktype = SOCK_STREAM;
    hints.ai_protocol = IPPROTO_TCP;
    hints.ai_flags = AI_PASSIVE;

    short int port = DEFAULT_PORT;
    while (!CheckIfPortAvailable(port)) {
        printf("Port %d not available\n", port);
        port++;
        if (port > 1110) {
            printf("Ports 1100-1110 not available\n");
            WSACleanup();
            return 1;
        }
    }

    std::ostringstream oss;
    oss << port;

    // Resolve the server address and port
    iResult = getaddrinfo("localhost", oss.str().c_str(), &hints, &serverinfo);
    if (iResult != 0) {
        printf("getaddrinfo failed with error: %d\n", iResult);
        WSACleanup();
        return 1;
    }

    // Create a SOCKET for connecting to server
    server_socket = socket(serverinfo->ai_family, serverinfo->ai_socktype, serverinfo->ai_protocol);
    if (server_socket == INVALID_SOCKET) {
        printf("socket failed with error: %ld\n", WSAGetLastError());
        freeaddrinfo(serverinfo);
        WSACleanup();
        return 1;
    }

    // Setup the TCP listening socket
    iResult = bind(server_socket, serverinfo->ai_addr, (int)serverinfo->ai_addrlen);
    if (iResult == SOCKET_ERROR) {
        printf("bind failed with error: %d\n", WSAGetLastError());
        freeaddrinfo(serverinfo);
        closesocket(server_socket);
        WSACleanup();
        return 1;
    }

    iResult = listen(server_socket, SOMAXCONN);
    if (iResult == SOCKET_ERROR) {
        printf("listen failed with error: %d\n", WSAGetLastError());
        closesocket(server_socket);
        WSACleanup();
        return 1;
    }

    server_thread = CreateThread(
        NULL,
        0,
        ServerThread,
        (LPVOID)server_socket,
        0,
        NULL
    );
}

void CVCamStream::CleanupServer() {
    freeaddrinfo(serverinfo);
    closesocket(server_socket);
    CloseHandle(server_thread);
    WSACleanup();
}

//
// Function: ServerThread
//
// Description:
//    This routine services a single server socket. For TCP this means accept
//    a client connection and then recv and send in a loop. When the client
//    closes the connection, wait for another client, etc. For UDP, we simply
//    sit in a loop and recv a datagram and echo it back to its source. For any
//    error, this routine exits.
//
DWORD WINAPI ServerThread(LPVOID lpParam)
{
    SOCKET client_socket = INVALID_SOCKET, server_socket_handle;
    SOCKADDR_STORAGE from;
    char servstr[NI_MAXSERV],
         hoststr[NI_MAXHOST];
    int socket_type,
        retval,
        fromlen,
        bytecount;

    // Retrieve the socket handle
    server_socket_handle = (SOCKET)lpParam;

    // Get the socket type back
    fromlen = sizeof(socket_type);
    retval = getsockopt(server_socket_handle, SOL_SOCKET, SO_TYPE, (char*)&socket_type, &fromlen);
    if (retval == INVALID_SOCKET)
    {
        fprintf(stderr, "getsockopt(SO_TYPE) failed: %d\n", WSAGetLastError());
        goto cleanup;
    }

    for (;;)
    {
        fromlen = sizeof(from);

        if (socket_type == SOCK_STREAM)
        {
            if (client_socket != INVALID_SOCKET)
            {
                //
                // If we have a client connection recv and send until done
                //
                bytecount = recv(client_socket, (char*)frameBuffer, BUFFER_LENGTH, 0);
                if (bytecount == SOCKET_ERROR)
                {
                    fprintf(stderr, "recv failed: %d\n", WSAGetLastError());
                    goto cleanup;
                }
                else if (bytecount == 0)
                {
                    // Client connection was closed
                    retval = shutdown(client_socket, SD_SEND);
                    if (retval == SOCKET_ERROR)
                    {
                        fprintf(stderr, "shutdown failed: %d\n", WSAGetLastError());
                        goto cleanup;
                    }

                    closesocket(client_socket);
                    client_socket = INVALID_SOCKET;
                }
            }
            else
            {
                //
                // No client connection so wait for one
                //
                client_socket = accept(server_socket_handle, (SOCKADDR*)&from, &fromlen);
                if (client_socket == INVALID_SOCKET)
                {
                    fprintf(stderr, "accept failed: %d\n", WSAGetLastError());
                    goto cleanup;
                }

                // Display the client's address
                retval = getnameinfo(
                    (SOCKADDR*)&from,
                    fromlen,
                    hoststr,
                    NI_MAXHOST,
                    servstr,
                    NI_MAXSERV,
                    NI_NUMERICHOST | NI_NUMERICSERV
                );
                if (retval != 0)
                {
                    fprintf(stderr, "getnameinfo failed: %d\n", retval);
                    goto cleanup;
                }

                printf("Accepted connection from host %s and port %s\n",
                    hoststr, servstr);
            }
        }
    }

cleanup:

    // Close the client connection if present
    if (client_socket != INVALID_SOCKET)
    {
        closesocket(client_socket);
        client_socket = INVALID_SOCKET;
    }

    return 0;
}

bool CheckIfPortAvailable(short int dwPort) {
    WSADATA wsaData;
    struct addrinfo* result = NULL,
        * ptr = NULL,
        hints;
    int sock;
    char buffer[4] = { 0 };

    int iResult = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (iResult != NO_ERROR) {
        printf("WSAStartup function failed with error: %d\n", iResult);
        return false;
    }

    ZeroMemory(&hints, sizeof(hints));
    hints.ai_family = AF_INET;
    hints.ai_socktype = SOCK_STREAM;
    hints.ai_protocol = IPPROTO_TCP;

    std::ostringstream oss;
    oss << dwPort;

    // Resolve the server address and port
    iResult = getaddrinfo("127.0.0.1", oss.str().c_str(), &hints, &result);
    if (iResult != 0) {
        printf("getaddrinfo failed with error: %d\n", iResult);
        WSACleanup();
        return 1;
    }

    sock = (int)socket(result->ai_family, result->ai_socktype, result->ai_protocol);
    if (sock == INVALID_SOCKET) {
        printf("ERROR: Socket function failed with error: %ld\n", WSAGetLastError());
        WSACleanup();
        return false;
    }

    printf("INFO: Checking Port : %s:%d\n", "127.0.0.1", dwPort);
    iResult = connect(sock, result->ai_addr, (int)result->ai_addrlen);

    freeaddrinfo(result);
    closesocket(sock);
    WSACleanup();

    if (iResult == SOCKET_ERROR) {
        printf("ERROR: %d", WSAGetLastError());
        return true;
    }
    else
    {
        return false;
    }
}

#pragma endregion
