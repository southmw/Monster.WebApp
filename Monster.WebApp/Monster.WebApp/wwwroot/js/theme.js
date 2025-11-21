window.themeManager = {
    saveTheme: function (isDark) {
        localStorage.setItem('isDarkMode', isDark.toString());
    },
    loadTheme: function () {
        const value = localStorage.getItem('isDarkMode');
        return value === 'true';
    }
};
