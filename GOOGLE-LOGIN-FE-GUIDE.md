# Hướng dẫn Frontend tích hợp Login Google

## 1. Google Client ID

```
573872884539-lov9g4rc77itiaucc7lovecrjel9bbnd.apps.googleusercontent.com
```

> **Lưu ý:** Backend chỉ cần `ClientId`, không cần `ClientSecret` — Google OAuth sử dụng xác thực bất đối xứng (asymmetric verification) qua public keys.

### Authorized JavaScript origins (fixing `origin_mismatch`)

If you see the Google error `origin_mismatch`, it means the web origin (protocol+host+port) your app is running on is not listed in the OAuth client's "Authorized JavaScript origins" in Google Cloud Console. Add the exact origin(s) for your frontend here.

- Common dev origins to add:
  - `http://localhost:3000`
  - `http://127.0.0.1:3000`
  - `http://localhost:5174` (used by some dev servers)

To find the exact origin in your browser, open DevTools Console on the page that runs the Google button and run:

```js
window.location.origin
```

Then copy the returned value and add it to the OAuth client's Authorized JavaScript origins.

Steps in Google Cloud Console:
1. Go to APIs & Services → Credentials.
2. Open the OAuth 2.0 Client ID that matches the `ClientId` shown above.
3. Under **Authorized JavaScript origins**, click **Add URI** and paste the origin (e.g. `http://localhost:3000`).
4. Save the client.

After saving, restart your dev server and retry Google Sign-In.

---

## 2. Luồng đăng nhập

```
┌──────────┐                         ┌──────────┐                    ┌──────────┐
│  Google   │                         │ Frontend │                    │ Backend  │
└────┬─────┘                         └────┬─────┘                    └────┬─────┘
     │                                     │                               │
     │    1. Hiển thị nút "Sign in         │                               │
     │       with Google"                  │                               │
     │◄────────────────────────────────────┤                               │
     │                                     │                               │
     │    2. User nhấn → Google trả        │                               │
     │       về credential (id_token)      │                               │
     ├────────────────────────────────────►│                               │
     │                                     │                               │
     │                                     │  3. POST /api/auth/login/google
     │                                     │     { "idToken": "eyJ..." }   │
     │                                     ├──────────────────────────────►│
     │                                     │                               │
     │                                     │  4. Backend verify token      │
     │                                     │     → Tự tạo user nếu chưa có│
     │                                     │     → Trả JWT access token    │
     │                                     │◄──────────────────────────────┤
     │                                     │                               │
     │    5. Lưu accessToken, dùng cho     │                               │
     │       Authorization header          │                               │
     │                                     │                               │
```

---

## 3. Cài đặt Google Sign-In (Frontend)

### Cách 1: Dùng thư viện `@react-oauth/google` (React — khuyến nghị)

```bash
npm install @react-oauth/google
```

**Wrap app với GoogleOAuthProvider:**

```tsx
import { GoogleOAuthProvider } from '@react-oauth/google';

const GOOGLE_CLIENT_ID = '573872884539-lov9g4rc77itiaucc7lovecrjel9bbnd.apps.googleusercontent.com';

function App() {
  return (
    <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
      {/* ... app routes ... */}
    </GoogleOAuthProvider>
  );
}
```

**Component nút Login:**

```tsx
import { GoogleLogin, CredentialResponse } from '@react-oauth/google';

function LoginPage() {
  const handleGoogleLogin = async (credentialResponse: CredentialResponse) => {
    const idToken = credentialResponse.credential;
    if (!idToken) return;

    const response = await fetch('/api/auth/login/google', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',           // quan trọng — nhận refresh token cookie
      body: JSON.stringify({ idToken }),
    });

    if (response.ok) {
      const data = await response.json();
      // data.accessToken  — JWT token
      // data.tokenType    — "Bearer"
      // data.expiresIn    — thời hạn (giây), mặc định 3600
      // data.user         — thông tin user
      console.log('Login thành công:', data);
      // Lưu accessToken vào memory / state management
    } else {
      const error = await response.json();
      console.error('Login thất bại:', error);
    }
  };

  return (
    <GoogleLogin
      onSuccess={handleGoogleLogin}
      onError={() => console.error('Google Sign-In failed')}
    />
  );
}
```

### Cách 2: Dùng Google Identity Services (GIS) trực tiếp (Vanilla JS / bất kỳ framework)

```html
<script src="https://accounts.google.com/gsi/client" async defer></script>

<div id="g_id_onload"
     data-client_id="573872884539-lov9g4rc77itiaucc7lovecrjel9bbnd.apps.googleusercontent.com"
     data-callback="handleGoogleLogin">
</div>
<div class="g_id_signin" data-type="standard"></div>

<script>
async function handleGoogleLogin(response) {
  const idToken = response.credential;

  const res = await fetch('/api/auth/login/google', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ idToken }),
  });

  if (res.ok) {
    const data = await res.json();
    // Lưu data.accessToken
  }
}
</script>
```

---

## 4. API Endpoint Chi Tiết

### `POST /api/auth/login/google`

| Thuộc tính     | Giá trị                      |
|----------------|------------------------------|
| Method         | `POST`                       |
| URL            | `/api/auth/login/google`     |
| Auth           | Không cần (AllowAnonymous)   |
| Content-Type   | `application/json`           |
| Rate Limiting  | Có (AuthPolicy)              |

#### Request Body

```json
{
  "idToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

| Field     | Type   | Required | Mô tả                                |
|-----------|--------|----------|---------------------------------------|
| `idToken` | string | ✅       | Google ID token nhận từ Google Sign-In |

#### Response — 200 OK (Thành công)

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "user": {
    "id": "a1b2c3d4-...",
    "email": "user@gmail.com",
    "displayName": "Nguyen Van A",
    "roles": ["User"]
  }
}
```

> **Refresh token** được trả qua `httpOnly` cookie — không nằm trong response body. Đảm bảo gửi `credentials: 'include'` trong mọi request.

#### Response — Lỗi

| Status | Khi nào                                    | Body mẫu                                          |
|--------|--------------------------------------------|----------------------------------------------------|
| 400    | Token không hợp lệ hoặc thiếu email       | `{ "error": "Invalid or expired token" }`          |
| 503    | Google provider chưa được bật trên backend | `{ "error": "Google login not enabled" }`          |
| 429    | Gọi quá nhiều lần (rate limit)             | —                                                  |

---

## 5. Sử dụng Access Token sau khi login

Sau khi login thành công, đính kèm `accessToken` vào header `Authorization` cho mọi API call:

```ts
const response = await fetch('/api/some-endpoint', {
  method: 'GET',
  headers: {
    'Authorization': `Bearer ${accessToken}`,
    'Content-Type': 'application/json',
  },
  credentials: 'include',  // để gửi kèm refresh token cookie
});
```

### Token hết hạn

- `accessToken` hết hạn sau **60 phút** (`expiresIn: 3600`).
- Refresh token (cookie) hết hạn sau **7 ngày**.
- Khi `accessToken` hết hạn (nhận 401), gọi endpoint refresh token để lấy token mới.

---

## 6. Lưu ý quan trọng

1. **Không lưu `accessToken` vào `localStorage`** — lưu trong memory/state để tránh XSS.
2. **Luôn gửi `credentials: 'include'`** để browser gửi/nhận cookie chứa refresh token.
3. **Nếu user chưa có tài khoản**, backend **tự động tạo** khi login Google lần đầu (email confirmed sẵn).
4. **ClientId phải khớp** giữa frontend và backend — nếu khác nhau, token verify sẽ thất bại (400).
