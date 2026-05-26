/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Pages/**/*.cshtml',
    './Views/**/*.cshtml',
    './Areas/**/*.cshtml'
  ],
  theme: {
    extend: {
      colors: {
        black: '#0a0a0a',
        gray: {
          50: '#fafafa',
          100: '#f4f4f4',
          200: '#e8e8e8',
          300: '#d4d4d4',
          400: '#a0a0a0',
          600: '#525252',
          700: '#404040',
          800: '#262626',
        },
        error: '#c0392b',
        draft: {
          bg: '#fff7e6',
          text: '#b54708',
        },
        private: {
          bg: '#f0f1ff',
          text: '#3b3f87',
        },
        published: {
          bg: '#e6ffed',
          text: '#1a7f37',
        },
        success: {
          bg: '#f0fdf4',
          text: '#166534',
          border: '#bbf7d0',
        },
        danger: {
          bg: '#fef2f2',
          border: '#fecaca',
        },
      },
      fontFamily: {
        display: ['"DM Serif Display"', 'Georgia', 'serif'],
        sans: ['"DM Sans"', 'system-ui', 'sans-serif'],
      },
      borderRadius: {
        DEFAULT: '2px',
        md: '4px',
      },
      transitionDuration: {
        DEFAULT: '180ms',
      },
      maxWidth: {
        content: '1120px',
      },
      height: {
        nav: '60px',
      },
      spacing: {
        nav: '60px',
      },
      boxShadow: {
        dropdown: '0 8px 20px rgba(2,6,23,0.08)',
        'btn-primary-hover': '0 4px 12px rgba(0,0,0,0.12)',
        'btn-danger-hover': '0 4px 12px rgba(192,57,43,0.18)',
      },
      keyframes: {
        'fade-in-up': {
          from: { opacity: '0', transform: 'translateY(-4px)' },
          to: { opacity: '1', transform: 'translateY(0)' },
        },
      },
      animation: {
        'fade-in-up': 'fade-in-up 0.18s ease',
      },
    },
  },
  plugins: [],
}
