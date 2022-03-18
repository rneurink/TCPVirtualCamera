#pragma once

#include <deque>

#define DECLARE_PTR(type, ptr, expr) type* ptr = (type*)(expr);

#define MIN_WIDTH 320
#define MIN_HEIGHT 240
#define MAX_WIDTH 1920
#define MAX_HEIGHT 1080
#define MAX_FRAMETIME 1000000
#define MIN_FRAMETIME 166666
#define SLEEP_DURATION 5

//#define TCP_SERVER
#define TCP_CLIENT

#ifndef TCP_SERVER
#ifndef TCP_CLIENT
#error Need to define a client or server
#endif
#endif

#ifdef TCP_SERVER
#ifdef TCP_CLIENT
#error Cannot use both client and server
#endif
#endif


EXTERN_C const GUID CLSID_VirtualCam;

class CVCamStream;

struct format
{
	format(int width_, int height_, int64_t time_per_frame_) {
		width = width_;
		height = height_;
		time_per_frame = time_per_frame_;
	}
	int width;
	int height;
	int64_t time_per_frame;
};

class CVCam : public CSource
{
public:
    //////////////////////////////////////////////////////////////////////////
    //  IUnknown
    //////////////////////////////////////////////////////////////////////////
    static CUnknown * WINAPI CreateInstance(LPUNKNOWN lpunk, HRESULT *phr);
    STDMETHODIMP NonDelegatingQueryInterface(REFIID riid, void **ppv);

    IFilterGraph *GetGraph() {return m_pGraph;}
    FILTER_STATE GetState(){ return m_State; }

    CVCam(LPUNKNOWN lpunk, HRESULT *phr);

private:
    CVCamStream *stream = nullptr;
};

class CVCamStream : public CSourceStream, public IAMStreamConfig, public IKsPropertySet
{
public:

    //////////////////////////////////////////////////////////////////////////
    //  IUnknown
    //////////////////////////////////////////////////////////////////////////
    STDMETHODIMP QueryInterface(REFIID riid, void **ppv);
    STDMETHODIMP_(ULONG) AddRef() { return GetOwner()->AddRef(); }                                                          \
    STDMETHODIMP_(ULONG) Release() { return GetOwner()->Release(); }

    //////////////////////////////////////////////////////////////////////////
    //  IQualityControl
    //////////////////////////////////////////////////////////////////////////
    STDMETHODIMP Notify(IBaseFilter * pSender, Quality q);

    //////////////////////////////////////////////////////////////////////////
    //  IAMStreamConfig
    //////////////////////////////////////////////////////////////////////////
    HRESULT STDMETHODCALLTYPE SetFormat(AM_MEDIA_TYPE *pmt);
    HRESULT STDMETHODCALLTYPE GetFormat(AM_MEDIA_TYPE **ppmt);
    HRESULT STDMETHODCALLTYPE GetNumberOfCapabilities(int *piCount, int *piSize);
    HRESULT STDMETHODCALLTYPE GetStreamCaps(int iIndex, AM_MEDIA_TYPE **pmt, BYTE *pSCC);

    //////////////////////////////////////////////////////////////////////////
    //  IKsPropertySet
    //////////////////////////////////////////////////////////////////////////
    HRESULT STDMETHODCALLTYPE Set(REFGUID guidPropSet, DWORD dwID, void *pInstanceData, DWORD cbInstanceData, void *pPropData, DWORD cbPropData);
    HRESULT STDMETHODCALLTYPE Get(REFGUID guidPropSet, DWORD dwPropID, void *pInstanceData,DWORD cbInstanceData, void *pPropData, DWORD cbPropData, DWORD *pcbReturned);
    HRESULT STDMETHODCALLTYPE QuerySupported(REFGUID guidPropSet, DWORD dwPropID, DWORD *pTypeSupport);
    
    //////////////////////////////////////////////////////////////////////////
    //  CSourceStream
    //////////////////////////////////////////////////////////////////////////
    CVCamStream(HRESULT *phr, CVCam *pParent, LPCWSTR pPinName);
    ~CVCamStream();

    HRESULT FillBuffer(IMediaSample *pms);
    HRESULT DecideBufferSize(IMemAllocator *pIMemAlloc, ALLOCATOR_PROPERTIES *pProperties);
    HRESULT CheckMediaType(const CMediaType *pMediaType);
    HRESULT GetMediaType(int iPosition, CMediaType *pmt);
    HRESULT SetMediaType(const CMediaType *pmt);
    HRESULT OnThreadCreate(void);
    HRESULT OnThreadDestroy(void);
    
private:
    void ListSupportFormat(void);
    bool ValidateResolution(long width, long height);

#ifdef TCP_SERVER
    int SetupServer();
    void CleanupServer();
#endif
#ifdef TCP_CLIENT
    int SetupClient();
    void CleanupClient();
#endif

    CVCam *m_pParent;
    REFERENCE_TIME m_rtLastTime;
    CCritSec m_cSharedState;
    IReferenceClock *m_pClock;

    std::deque<format> format_list;

#ifdef TCP_SERVER
    struct addrinfo* serverinfo = NULL;
    HANDLE server_thread = NULL;
#endif
#ifdef TCP_CLIENT
    struct addrinfo* clientinfo = NULL;
    HANDLE client_thread = NULL;
#endif

    int currentformatIndex;
};


