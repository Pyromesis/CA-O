// CA-O Service Worker for PWA Support
const CACHE_NAME = 'ca-o-v1';
const STATIC_ASSETS = [
  '/',
  '/manifest.json',
];

// Install event - cache static assets
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => {
        console.log('CA-O: Caching static assets');
        return cache.addAll(STATIC_ASSETS);
      })
      .then(() => self.skipWaiting())
  );
});

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((cacheNames) =>
        Promise.all(
          cacheNames
            .filter((name) => name !== CACHE_NAME)
            .map((name) => caches.delete(name))
        )
      )
      .then(() => self.clients.claim())
  );
});

// Fetch event - network first, fallback to cache
self.addEventListener('fetch', (event) => {
  // Skip non-GET requests and external URLs
  if (event.request.method !== 'GET') return;
  
  if (!event.request.url.startsWith(self.location.origin)) return;

  event.respondWith(
    fetch(event.request)
      .then((response) => {
        // Clone successful response and add to cache
        const responseClone = response.clone();
        caches.open(CACHE_NAME)
          .then((cache) => cache.put(event.request, responseClone));
        
        return response;
      })
      .catch(() => {
        // Fallback to cache if network fails
        return caches.match(event.request).then((cachedResponse) => {
          if (cachedResponse) {
            return cachedResponse;
          }
          
          // Return offline page if available
          return new Response(
            '<!DOCTYPE html><html><head><meta charset="utf-8"><title>Offline - CA-O</title><style>body{display:flex;align-items:center;justify-content:center;height:100vh;margin:0;background:#0a0a14;color:#fff;font-family:sans-serif;text-align:center;padding:20px}</style></head><body><div><h1 style="color:#FF6B35;font-size:48px;margin-bottom:16px">🔌</h1><p>You are currently offline</p><p style="opacity:0.5;margin-top:8px">Please check your connection</p></div></body></html>',
            { headers: { 'Content-Type': 'text/html' } }
          );
        });
      })
  );
});

// Handle push notifications (future feature)
self.addEventListener('push', (event) => {
  const data = event.json?.data || {};
  
  const options = {
    body: data.body || 'New optimization completed!',
    icon: '/icons/icon-192x192.png',
    badge: '/icons/icon-72x72.png',
    vibrate: [100, 50, 100],
    data: {
      url: data.url || '/',
    },
  };
  
  event.waitUntil(
    self.registration.showNotification('CA-O', options)
  );
});

// Notification click handler
self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  
  event.waitUntil(
    clients.openWindow(event.notification.data?.url || '/')
  );
});

console.log('✅ CA-O Service Worker loaded');
