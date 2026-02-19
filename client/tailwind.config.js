/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        bg: '#f3f4f6',
        panel: '#ffffff',
        accent: '#0f766e',
        accentSoft: '#ccfbf1',
        danger: '#b91c1c'
      },
      fontFamily: {
        sans: ['Segoe UI', 'system-ui', 'sans-serif']
      }
    },
  },
  plugins: [],
};
