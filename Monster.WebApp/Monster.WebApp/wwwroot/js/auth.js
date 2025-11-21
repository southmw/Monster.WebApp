// Login function that calls the API from the browser
window.loginUser = async function (username, password) {
    try {
        const response = await fetch('/api/auth/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ username, password }),
            credentials: 'include' // Important: include cookies
        });

        if (response.ok) {
            const data = await response.json();
            return {
                success: true,
                displayName: data.displayName,
                message: data.message
            };
        } else {
            return {
                success: false,
                message: '사용자명 또는 비밀번호가 올바르지 않습니다.'
            };
        }
    } catch (error) {
        return {
            success: false,
            message: '로그인 중 오류가 발생했습니다: ' + error.message
        };
    }
};
