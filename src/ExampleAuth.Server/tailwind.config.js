/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./Pages/**/*.cshtml'],
  theme: {
    extend: {
      colors: {
        canvas: '#0A1622',
        surface: {
          DEFAULT: '#101F30',
          raised: '#16283C',
        },
        border: {
          DEFAULT: '#223449',
          strong: '#2C4159',
        },
        ink: {
          primary: '#EAF1F7',
          secondary: '#9FB3C8',
          tertiary: '#5E7893',
        },
        brand: {
          blue: '#004A75',
          'blue-bright': '#4FA3D9',
          orange: '#E77A27',
          'orange-bright': '#F2955A',
          'orange-dim': '#C4631A',
        },
        success: {
          DEFAULT: '#34A870',
          bg: '#12301F',
          text: '#5FD79A',
        },
        danger: {
          DEFAULT: '#E5484D',
          bg: '#331418',
          text: '#FF8A8F',
        },
      },
      fontFamily: {
        sans: ['system-ui', '-apple-system', '"Segoe UI"', 'Roboto', 'sans-serif'],
        mono: ['ui-monospace', 'SFMono-Regular', 'Menlo', 'Consolas', 'monospace'],
      },
    },
  },
  plugins: [],
};
