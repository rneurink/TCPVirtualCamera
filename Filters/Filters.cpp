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

#define BUFFER_LENGTH (MAX_WIDTH * MAX_HEIGHT * 3)

byte* framePointer;
byte frameBuffer[BUFFER_LENGTH];

#ifdef TCP_SERVER
DWORD WINAPI ServerThread(LPVOID lpParam);
bool CheckIfPortAvailable(short int dwPort);

SOCKET server_socket = INVALID_SOCKET;
#endif // TCP_SERVER

struct TCPClientInfo
{
    TCPClientInfo() {
        client_socket = INVALID_SOCKET;
        addressinfo = NULL;
    }
    TCPClientInfo(SOCKET socket_, struct addrinfo* addressinfo_) {
        client_socket = socket_;
        addressinfo = addressinfo_;
    }
    SOCKET client_socket;
    struct addrinfo* addressinfo = NULL;
};

#ifdef TCP_CLIENT
DWORD WINAPI ClientThread(LPVOID lpParam);

SOCKET client_socket = INVALID_SOCKET;
TCPClientInfo client_info;
#endif // TCP_CLIENT

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
    CSource(NAME("Virtual TCP Cam"), lpunk, CLSID_VirtualCam) // Source name
{
    //DbgLog((LOG_TRACE, 0, TEXT("Creating camera object"))); // not working somehow
    //DbgOutString("Hello World"); // Compile error

    ASSERT(phr);
    CAutoLock cAutoLock(&m_cStateLock);
    // Create the one and only output pin
    m_paStreams = (CSourceStream **) new CVCamStream*[1];
    stream = new CVCamStream(phr, this, L"Virtual Video"); // Pinname
    m_paStreams[0] = stream;
}

HRESULT CVCam::NonDelegatingQueryInterface(REFIID riid, void **ppv)
{
    //Forward request for IAMStreamConfig & IKsPropertySet to the pin
    if(riid == _uuidof(IAMStreamConfig) || riid == _uuidof(IKsPropertySet))
        return m_paStreams[0]->QueryInterface(riid, ppv);
    else
        return CSource::NonDelegatingQueryInterface(riid, ppv);
}

//////////////////////////////////////////////////////////////////////////
// CVCamStream is the one and only output pin of CVCam which handles 
// all the stuff.
//////////////////////////////////////////////////////////////////////////
CVCamStream::CVCamStream(HRESULT *phr, CVCam *pParent, LPCWSTR pPinName) :
    CSourceStream(NAME("Virtual Video"),phr, pParent, pPinName), m_pParent(pParent)
{
    ListSupportFormat(); // Create a list of supported formats

    // Set the default media type as 1920x1080 30Hz
    GetMediaType(0, &m_mt);
    
#ifdef TCP_SERVER
    //DbgLog((LOG_CUSTOM1, 1, TEXT("Starting server")));
    SetupServer();
    //DbgLog((LOG_TRACE, 3, TEXT("test string")));
#endif
#ifdef TCP_CLIENT
    SetupClient();
#endif
}

CVCamStream::~CVCamStream()
{
#ifdef TCP_SERVER
    CleanupServer();
#endif
#ifdef TCP_CLIENT
    CleanupClient();
#endif
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


void CVCamStream::ListSupportFormat()
{
    if (format_list.size() > 0)
        format_list.empty();
    
    format_list.push_back(struct format(1920, 1080, 333333)); // 1920x1080 30Hz
    format_list.push_back(struct format(1280, 720, 333333)); // 1280x720 30Hz
    format_list.push_back(struct format(480, 360, 333333)); // 480x360 30Hz 
    format_list.push_back(struct format(320, 240, 333333)); // 320x240 30Hz
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

    memcpy_s(pData, lDataLen, framePointer, lDataLen);

    return NOERROR;
} // FillBuffer

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
    for (int i = 0; i < format_list.size() - 1; i++ ) {
        if (pvi->bmiHeader.biWidth == format_list[i].width) {
            currentformatIndex = i;
            break;
        }
    }
    //DbgLog((LOG_TRACE, 0, TEXT("Setting media type %i"), currentformatIndex));
    //DbgLog((LOG_TRACE, 1, TEXT("Setting media type %i"), currentformatIndex));
    return hr;
}

// See Directshow help topic for IAMStreamConfig for details on this method
HRESULT CVCamStream::GetMediaType(int iPosition, CMediaType *pmt)
{
    if (format_list.size() == 0)
        ListSupportFormat();
    
    if(iPosition < 0) return E_INVALIDARG;
    if(iPosition > format_list.size()-1) return VFW_S_NO_MORE_ITEMS;

    DECLARE_PTR(VIDEOINFOHEADER, pvi, pmt->AllocFormatBuffer(sizeof(VIDEOINFOHEADER)));
    ZeroMemory(pvi, sizeof(VIDEOINFOHEADER));

    pvi->bmiHeader.biCompression = BI_RGB;
    pvi->bmiHeader.biBitCount    = 24;
    pvi->bmiHeader.biSize       = sizeof(BITMAPINFOHEADER);
    pvi->bmiHeader.biWidth      = format_list[iPosition].width;
    pvi->bmiHeader.biHeight     = format_list[iPosition].height;
    pvi->bmiHeader.biPlanes     = 1;
    pvi->bmiHeader.biSizeImage  = GetBitmapSize(&pvi->bmiHeader);
    pvi->bmiHeader.biClrImportant = 0;

    pvi->AvgTimePerFrame = format_list[iPosition].time_per_frame;

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
    if (pMediaType == nullptr)
        return E_FAIL;

    VIDEOINFOHEADER *pvi = (VIDEOINFOHEADER *)(pMediaType->Format());

    const GUID* type = pMediaType->Type();
    const GUID* info = pMediaType->FormatType();
    const GUID* subtype = pMediaType->Subtype();

    if (*type != MEDIATYPE_Video)
        return E_INVALIDARG;

    if (*info != FORMAT_VideoInfo)
        return E_INVALIDARG;

    if (*subtype != MEDIASUBTYPE_RGB24)
        return E_INVALIDARG;

    if (pvi->AvgTimePerFrame < MIN_FRAMETIME || pvi->AvgTimePerFrame > MAX_FRAMETIME)
        return E_INVALIDARG;

    if (ValidateResolution(pvi->bmiHeader.biWidth, pvi->bmiHeader.biHeight))
        return S_OK;

    return E_INVALIDARG;
} // CheckMediaType

bool CVCamStream::ValidateResolution(long width,long height)
{
    if (width < MIN_WIDTH || height < MIN_HEIGHT) // Check minimal
        return false;
    else if (width > MAX_WIDTH) // Check max width
        return false;
    else if (width * 9 == height * 16) // Aspect 16:9
        return true;
    else if (width * 3 == height * 4) // Aspect 4:3
        return true;
    else
        return false;
}

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

// Called when the graph is run
HRESULT CVCamStream::OnThreadCreate()
{
    m_rtLastTime = 0;
    return NOERROR;
} // OnThreadCreate

// Called when the graph is destroyed
HRESULT CVCamStream::OnThreadDestroy()
{
    return NOERROR;
}


//////////////////////////////////////////////////////////////////////////
//  IAMStreamConfig
//////////////////////////////////////////////////////////////////////////

HRESULT STDMETHODCALLTYPE CVCamStream::SetFormat(AM_MEDIA_TYPE *pmt)
{
    if (pmt == nullptr)
        return E_FAIL;

    if (m_pParent->GetState() != State_Stopped)
        return E_FAIL;

    if (CheckMediaType((CMediaType *)pmt) != S_OK)
        return E_FAIL;

    DECLARE_PTR(VIDEOINFOHEADER, pvi, m_mt.pbFormat);
    format_list.push_front(struct format(pvi->bmiHeader.biWidth, 
        pvi->bmiHeader.biHeight, pvi->AvgTimePerFrame));

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
    if (format_list.size() == 0)
        ListSupportFormat();
    
    *piCount = format_list.size();
    *piSize = sizeof(VIDEO_STREAM_CONFIG_CAPS);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CVCamStream::GetStreamCaps(int iIndex, AM_MEDIA_TYPE **pmt, BYTE *pSCC)
{
    if (format_list.size() == 0)
        ListSupportFormat();

    if(iIndex < 0) return E_INVALIDARG;
    if(iIndex > format_list.size()-1) return VFW_S_NO_MORE_ITEMS;

    *pmt = CreateMediaType(&m_mt);
    DECLARE_PTR(VIDEOINFOHEADER, pvi, (*pmt)->pbFormat);

    pvi->bmiHeader.biCompression = BI_RGB;
    pvi->bmiHeader.biBitCount    = 24;
    pvi->bmiHeader.biSize       = sizeof(BITMAPINFOHEADER);
    pvi->bmiHeader.biWidth      = format_list[iIndex].width;
    pvi->bmiHeader.biHeight     = format_list[iIndex].height;
    pvi->bmiHeader.biPlanes     = 1;
    pvi->bmiHeader.biSizeImage  = GetBitmapSize(&pvi->bmiHeader);
    pvi->bmiHeader.biClrImportant = 0;

    pvi->AvgTimePerFrame = format_list[iIndex].time_per_frame;

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
    pvscc->InputSize.cx = pvi->bmiHeader.biWidth;
    pvscc->InputSize.cy = pvi->bmiHeader.biHeight;
    pvscc->MinCroppingSize.cx = pvi->bmiHeader.biWidth;
    pvscc->MinCroppingSize.cy = pvi->bmiHeader.biHeight;
    pvscc->MaxCroppingSize.cx = pvi->bmiHeader.biWidth;
    pvscc->MaxCroppingSize.cy = pvi->bmiHeader.biHeight;
    pvscc->CropGranularityX = pvi->bmiHeader.biWidth;
    pvscc->CropGranularityY = pvi->bmiHeader.biHeight;
    pvscc->CropAlignX = 0;
    pvscc->CropAlignY = 0;

    pvscc->MinOutputSize.cx = pvi->bmiHeader.biWidth;
    pvscc->MinOutputSize.cy = pvi->bmiHeader.biHeight;
    pvscc->MaxOutputSize.cx = pvi->bmiHeader.biWidth;
    pvscc->MaxOutputSize.cy = pvi->bmiHeader.biHeight;
    pvscc->OutputGranularityX = 0;
    pvscc->OutputGranularityY = 0;
    pvscc->StretchTapsX = 0;
    pvscc->StretchTapsY = 0;
    pvscc->ShrinkTapsX = 0;
    pvscc->ShrinkTapsY = 0;
    pvscc->MinFrameInterval = pvi->AvgTimePerFrame;
    pvscc->MaxFrameInterval = pvi->AvgTimePerFrame;
    pvscc->MinBitsPerSecond = pvi->bmiHeader.biWidth * pvi->bmiHeader.biHeight * 3 * 8 * (10000000 / pvi->AvgTimePerFrame);
    pvscc->MaxBitsPerSecond = pvi->bmiHeader.biWidth * pvi->bmiHeader.biHeight * 3 * 8 * (10000000 / pvi->AvgTimePerFrame);

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
#ifdef TCP_SERVER

int CVCamStream::SetupServer() {
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
    int retval,
        fromlen,
        bytecount;

    // Retrieve the socket handle
    server_socket_handle = (SOCKET)lpParam;

    for (;;) {
        if (client_socket != INVALID_SOCKET) {
            //
            // If we have a client connection recv and send until done
            //
            bytecount = recv(client_socket, (char*)frameBuffer, BUFFER_LENGTH, 0);
            if (bytecount == SOCKET_ERROR) {
                fprintf(stderr, "recv failed: %d\n", WSAGetLastError());
                closesocket(client_socket);
                client_socket = INVALID_SOCKET;
            }
            else if (bytecount == 0) {
                // Client connection was closed
                retval = shutdown(client_socket, SD_SEND);
                if (retval == SOCKET_ERROR) {
                    fprintf(stderr, "shutdown failed: %d\n", WSAGetLastError());
                }

                closesocket(client_socket);
                client_socket = INVALID_SOCKET;
            }

        } else {
            fromlen = sizeof(from);
            //
            // No client connection so wait for one
            //
            client_socket = accept(server_socket_handle, (SOCKADDR*)&from, &fromlen);
            if (client_socket == INVALID_SOCKET) {
                fprintf(stderr, "accept failed: %d\n", WSAGetLastError());
                closesocket(client_socket);
                client_socket = INVALID_SOCKET;
            }
        }
    }

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

#endif
#pragma endregion

#pragma region TCP client
#ifdef TCP_CLIENT

int CVCamStream::SetupClient() {
    framePointer = &frameBuffer[0];

    WSAData wsaData;
    int iResult;
    SOCKET client_socket = INVALID_SOCKET;
    struct addrinfo* clientinfo = NULL;

    struct addrinfo hints;

    // Initialize Winsock
    iResult = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (iResult != 0) {
        fprintf(stderr, "WSAStartup failed with error: %d\n", iResult);
        return 1;
    }

    ZeroMemory(&hints, sizeof(hints));
    hints.ai_family = AF_INET;
    hints.ai_socktype = SOCK_STREAM;
    hints.ai_protocol = IPPROTO_TCP;

    std::ostringstream oss;
    oss << DEFAULT_PORT;

    // Resolve the server address and port
    iResult = getaddrinfo("localhost", oss.str().c_str(), &hints, &clientinfo);
    if (iResult != 0) {
        fprintf(stderr, "getaddrinfo failed with error: %d\n", iResult);
        WSACleanup();
        return 1;
    }

    client_info = TCPClientInfo(client_socket, clientinfo);
    
    client_thread = CreateThread(
        NULL,
        0,
        ClientThread,
        (LPVOID)&client_info,
        0,
        NULL
    );
}

void CVCamStream::CleanupClient() {
    freeaddrinfo(client_info.addressinfo);
    closesocket(client_info.client_socket);
    CloseHandle(client_thread);
    WSACleanup();
}

DWORD WINAPI ClientThread(LPVOID lpParam) {
    TCPClientInfo* client_info_handle;

    int retval,
        bytecount;

    // Retrieve the socket handle
    client_info_handle = (TCPClientInfo*)lpParam;
    SOCKET client_socket_handle = client_info_handle->client_socket;

    for(;;) {
        if (client_socket_handle != INVALID_SOCKET) {
            //TODO: add method to request image and set format etc.

            // Receive until the peer closes the connection
            bytecount = recv(client_socket_handle, (char*)frameBuffer, BUFFER_LENGTH, 0);
            if (bytecount == SOCKET_ERROR) {
                // Lost connection
                fprintf(stderr, "recv failed with error: %d\n", WSAGetLastError());
                closesocket(client_socket_handle);
                client_socket_handle = INVALID_SOCKET;
            }
            else if (bytecount == 0) {
                // shutdown the connection since no more data will be sent
                retval = shutdown(client_socket_handle, SD_SEND);
                if (retval == SOCKET_ERROR) {
                    fprintf(stderr, "shutdown failed with error: %d\n", WSAGetLastError());
                    closesocket(client_socket_handle);
                    client_socket_handle = INVALID_SOCKET;
                }
                client_socket_handle = INVALID_SOCKET;
            }

        } else {
            // Create a SOCKET for connecting to server
            client_socket_handle = socket(client_info_handle->addressinfo->ai_family, client_info_handle->addressinfo->ai_socktype,
                client_info_handle->addressinfo->ai_protocol);
            if (client_socket_handle == INVALID_SOCKET) {
                fprintf(stderr, "socket failed with error: %ld\n", WSAGetLastError());
                closesocket(client_socket_handle);
                client_socket_handle = INVALID_SOCKET;
            }

            // Connect to server.
            retval = connect(client_socket_handle, client_info_handle->addressinfo->ai_addr, (int)client_info_handle->addressinfo->ai_addrlen);
            if (retval == SOCKET_ERROR) {
                closesocket(client_socket_handle);
                client_socket_handle = INVALID_SOCKET;
                continue;
            }
        }
    }

    // Close the connection socket if needed
    if (client_socket_handle != INVALID_SOCKET) {
        closesocket(client_socket_handle);
        client_socket_handle = INVALID_SOCKET;
    }

    return 0;
}

#endif
#pragma endregion
