// Quill 에디터 미디어 헬퍼.
// window.Spillgebees.editors: Spillgebees.Blazor.RichTextEditor가 노출하는
// Map<HTMLElement(에디터 컨테이너), Quill> 전역.
window.monsterEditor = {
    // 툴바 이미지 버튼의 기본 핸들러(base64 본문 삽입)를 서버 업로드 방식으로 교체.
    // 업로드는 /api/fileupload (쿠키 인증 자동 전송, [Authorize] — 로그인 사용자 전용 버튼).
    // 에디터 초기화가 비동기(OnAfterRender JS interop)라 폴링으로 인스턴스를 기다린다.
    registerImageHandler: function (editorContainerId) {
        const tryAttach = (attempt) => {
            const el = document.getElementById(editorContainerId);
            const quill = el && window.Spillgebees?.editors?.get(el);
            if (!quill) {
                if (attempt < 50) {
                    setTimeout(() => tryAttach(attempt + 1), 100);
                }
                return;
            }
            quill.getModule('toolbar').addHandler('image', () => {
                const input = document.createElement('input');
                input.type = 'file';
                input.accept = 'image/jpeg,image/png,image/gif,image/webp';
                input.onchange = async () => {
                    const file = input.files && input.files[0];
                    if (!file) return;
                    const form = new FormData();
                    form.append('file', file);
                    try {
                        const res = await fetch('/api/fileupload', { method: 'POST', body: form });
                        const json = await res.json();
                        if (res.ok && json.success) {
                            const range = quill.getSelection(true);
                            quill.insertEmbed(range.index, 'image', json.url, 'user');
                            quill.setSelection(range.index + 1);
                        } else {
                            alert(json.errorMessage ?? '이미지 업로드에 실패했습니다.');
                        }
                    } catch {
                        alert('이미지 업로드 중 오류가 발생했습니다.');
                    }
                };
                input.click();
            });
        };
        tryAttach(0);
    }
};
