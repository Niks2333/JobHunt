"""End-to-end auth and ownership verification against the local API and SQL Server.

Uses only Python standard libraries. Test accounts and their data are removed in finally.
Run with the bundled Python runtime; optional first argument is the API URL.
"""
import base64
import concurrent.futures
import hashlib
import hmac
import http.cookiejar
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timedelta, timezone

BASE = sys.argv[1] if len(sys.argv) > 1 else 'http://localhost:5077'
ROOT = Path(__file__).resolve().parent.parent
MARKER = 'auth-test-' + uuid.uuid4().hex
EMAILS = [MARKER + '-a@example.com', MARKER + '-b@example.com']
PASSWORD = 'StudyPass!2026'

def sql(query):
    result = subprocess.run(['sqlcmd', '-S', 'localhost', '-d', 'JobHunt', '-E', '-C', '-I', '-b',
        '-h', '-1', '-W', '-Q', 'SET NOCOUNT ON; ' + query], capture_output=True, text=True)
    if result.returncode: raise AssertionError('SQL verification failed: ' + result.stdout + result.stderr)
    return result.stdout.strip()

class Client:
    def __init__(self):
        self.cookies = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cookies))
        self.token = None

    def call(self, path, method='GET', body=None, expected=200, headers=None, csrf=True, bearer=True):
        request_headers = {'Content-Type': 'application/json'}
        if csrf: request_headers['X-JobHunt-CSRF'] = '1'
        if bearer and self.token: request_headers['Authorization'] = 'Bearer ' + self.token
        request_headers.update(headers or {})
        request = urllib.request.Request(BASE + path, method=method, headers=request_headers,
            data=json.dumps(body).encode() if body is not None else None)
        try: response = self.opener.open(request)
        except urllib.error.HTTPError as error: response = error
        content = response.read()
        assert response.status == expected, f'{method} {path}: expected {expected}, got {response.status}'
        return json.loads(content) if content else None

    def login(self, email):
        result = self.call('/api/auth/login', 'POST', {'email': email, 'password': PASSWORD})
        self.token = result['accessToken']
        assert set(result) == {'accessToken', 'expiresAtUtc', 'user'}
        assert result['user']['email'] == email
        return result

    def cookie(self):
        return next(cookie for cookie in self.cookies if cookie.name == 'jobhunt.refresh')

def encode(value):
    return base64.urlsafe_b64encode(json.dumps(value, separators=(',', ':')).encode()).rstrip(b'=').decode()

def test():
    baseline = sql('SELECT COUNT(*) FROM dbo.JobApplications;')
    a, b = Client(), Client()
    for path in ['/api/applications', '/api/analytics/applications', '/api/interviews/upcoming', '/api/auth/me']:
        a.call(path, expected=401)
    a.call('/api/health')
    ids = []
    for email in EMAILS:
        response = a.call('/api/auth/register', 'POST', {'email': email, 'password': PASSWORD}, 201)
        ids.append(response['id'])
    a.call('/api/auth/login', 'POST', {'email': EMAILS[0], 'password': PASSWORD}, 403, csrf=False)
    a.call('/api/auth/login', 'POST', {'email': EMAILS[0], 'password': PASSWORD}, 403,
        headers={'Origin': 'https://untrusted.example'})
    a.call('/api/auth/login', 'POST', {'email': EMAILS[0], 'password': 'WrongPassword!2026'}, 401)
    session = a.login(EMAILS[0]); b.login(EMAILS[1])
    assert a.call('/api/auth/me')['id'] == ids[0]
    cookie = a.cookie()
    cookie_attributes = {key.lower(): value for key, value in cookie._rest.items()}
    assert cookie.path == '/api/auth' and 'httponly' in cookie_attributes
    assert cookie_attributes.get('samesite', '').lower() == 'strict'
    assert a.call('/api/applications')['totalApplications'] == 0, 'Legacy records must not leak.'

    app_body = {'companyName': MARKER, 'roleTitle': 'Auth verification', 'applicationDate': '2001-01-01',
        'source': MARKER, 'userId': ids[1]}
    app = a.call('/api/applications', 'POST', app_body, 201)
    app_id = app['jobApplicationId']
    assert sql(f'SELECT UserId FROM dbo.JobApplications WHERE JobApplicationId = {app_id};') == ids[0]
    assert a.call('/api/applications')['totalApplications'] == 1
    assert b.call('/api/applications')['totalApplications'] == 0
    assert b.call('/api/applications?source=' + MARKER)['availableSources'] == []
    a.call(f'/api/applications/{app_id}', 'PUT', {**app_body, 'roleTitle': 'Updated'})
    a.call(f'/api/applications/{app_id}/status-changes', 'POST', {'status': 'Interview', 'occurredOn': '2001-01-02'})
    interview_body = {'roundName': 'Technical', 'scheduledAt': (datetime.now(timezone.utc) + timedelta(days=2)).isoformat(), 'mode': 'Online'}
    interview = a.call(f'/api/applications/{app_id}/interviews', 'POST', interview_body, 201)
    round_id = interview['jobApplicationInterviewId']
    for method, path, body in [
        ('GET', f'/api/applications/{app_id}', None),
        ('PUT', f'/api/applications/{app_id}', app_body),
        ('POST', f'/api/applications/{app_id}/status-changes', {'status': 'Offer', 'occurredOn': '2001-01-03'}),
        ('GET', f'/api/applications/{app_id}/interviews', None),
        ('POST', f'/api/applications/{app_id}/interviews', interview_body),
        ('PUT', f'/api/applications/{app_id}/interviews/{round_id}', interview_body),
    ]: b.call(path, method, body, 404)
    assert a.call('/api/analytics/applications')['totalApplications'] == 1
    assert b.call('/api/analytics/applications')['totalApplications'] == 0
    assert a.call('/api/interviews/upcoming')['totalUpcomingInterviews'] == 1
    assert b.call('/api/interviews/upcoming')['totalUpcomingInterviews'] == 0
    assert len(a.call(f'/api/applications/{app_id}')['statusHistory']) == 2

    # Verify signature, issuer, audience, and expiry independently with signed test tokens.
    secrets_file = Path(os.environ['APPDATA']) / 'Microsoft/UserSecrets/jobhunt-api-development/secrets.json'
    secret = os.environ.get('Jwt__SigningKey') or json.loads(secrets_file.read_text(encoding='utf-8-sig'))['Jwt:SigningKey']
    payload = {'sub': ids[0], 'iss': 'JobHunt.Api', 'aud': 'JobHunt.Web', 'exp': int(time.time()) + 600}
    for change in [{'exp': int(time.time()) - 60}, {'iss': 'wrong'}, {'aud': 'wrong'}]:
        content = encode({'alg': 'HS256', 'typ': 'JWT'}) + '.' + encode({**payload, **change})
        signature = base64.urlsafe_b64encode(hmac.new(secret.encode(), content.encode(), hashlib.sha256).digest()).rstrip(b'=').decode()
        a.call('/api/applications', expected=401, headers={'Authorization': 'Bearer ' + content + '.' + signature})
    a.call('/api/applications', expected=401, headers={'Authorization': 'Bearer ' + session['accessToken'][:-8] + 'tampered'})

    old_cookie = a.cookie().value
    refreshed = a.call('/api/auth/refresh', 'POST', {})
    assert refreshed['accessToken'] != session['accessToken'] and a.cookie().value != old_cookie
    assert sql(f"SELECT COUNT(*) FROM dbo.RefreshTokens WHERE UserId = '{ids[0]}' AND RevokedAtUtc IS NULL;") == '1'
    replay = Client()
    replay.call('/api/auth/refresh', 'POST', {}, 401, headers={'Cookie': 'jobhunt.refresh=' + old_cookie})
    a.call('/api/auth/refresh', 'POST', {}, 401)
    a.login(EMAILS[0])
    logout_cookie = a.cookie().value
    a.call('/api/auth/logout', 'POST', {}, 204)
    a.call('/api/auth/refresh', 'POST', {}, 401)
    replay.call('/api/auth/refresh', 'POST', {}, 401, headers={'Cookie': 'jobhunt.refresh=' + logout_cookie})

    # Concurrent cookie rotation permits one refresh; reuse invalidates the resulting family.
    a.login(EMAILS[0]); concurrent_cookie = a.cookie().value
    def rotate(_):
        client = Client()
        request = urllib.request.Request(BASE + '/api/auth/refresh', method='POST', data=b'{}', headers={
            'Content-Type': 'application/json', 'X-JobHunt-CSRF': '1', 'Cookie': 'jobhunt.refresh=' + concurrent_cookie})
        try: return client.opener.open(request).status
        except urllib.error.HTTPError as error: return error.code
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        assert sorted(pool.map(rotate, range(2))) == [200, 401]
    assert sql(f"SELECT COUNT(*) FROM dbo.RefreshTokens WHERE UserId = '{ids[0]}' AND RevokedAtUtc IS NULL;") == '0'

    # Expired refresh tokens and account lockout must also prevent renewal/login.
    a.login(EMAILS[0])
    sql(f"UPDATE dbo.RefreshTokens SET ExpiresAtUtc = DATEADD(day, -1, SYSUTCDATETIME()) WHERE UserId = '{ids[0]}';")
    a.call('/api/auth/refresh', 'POST', {}, 401)
    for _ in range(5):
        b.call('/api/auth/login', 'POST', {'email': EMAILS[1], 'password': 'WrongPassword!2026'}, 401)
    b.call('/api/auth/login', 'POST', {'email': EMAILS[1], 'password': PASSWORD}, 401)
    b.call('/api/auth/refresh', 'POST', {}, 401)

    # Run existing feature checks with a valid token; their SQL totals are scoped to this user.
    a.login(EMAILS[0])
    env = dict(os.environ, JOBHUNT_TEST_ACCESS_TOKEN=a.token)
    for script in ['Test-JobApplicationFlow.ps1', 'Test-UpdateJobApplicationFlow.ps1',
        'Test-ChangeJobApplicationStatusFlow.ps1', 'Test-JobApplicationFiltering.ps1',
        'Test-ApplicationAnalytics.ps1', 'Test-JobApplicationInterviews.ps1']:
        result = subprocess.run(['pwsh', '-NoProfile', '-File', str(ROOT / 'scripts' / script), '-BackendUrl', BASE],
            env=env, capture_output=True, text=True)
        assert result.returncode == 0, script + ': ' + result.stdout + result.stderr
        print(result.stdout.strip())
    assert sql('SELECT COUNT(*) FROM dbo.JobApplications;') == str(int(baseline) + 1)
    # Invalid registration requests consume the auth limit without creating more test data.
    limited = False
    for _ in range(61):
        request = urllib.request.Request(BASE + '/api/auth/register', method='POST', data=b'{}',
            headers={'Content-Type': 'application/json'})
        try: status = a.opener.open(request).status
        except urllib.error.HTTPError as error: status = error.code
        assert status in (400, 429), 'Expected validation rejection or auth rate limiting.'
        if status == 429:
            limited = True
            break
    assert limited, 'Auth endpoints must limit repeated requests.'
    print('PASS: JWT validation, login and lockout, CSRF, me, refresh rotation/reuse/concurrency/expiry, logout, and user isolation for applications/history/interviews/analytics.')
    print('PASS: repeated auth requests return HTTP 429 at the configured rate limit.')

try:
    test()
finally:
    emails = ','.join("'" + email + "'" for email in EMAILS)
    sql(f'DELETE FROM dbo.JobApplications WHERE UserId IN (SELECT Id FROM dbo.AspNetUsers WHERE Email IN ({emails})); '
        f'DELETE FROM dbo.AspNetUsers WHERE Email IN ({emails});')
