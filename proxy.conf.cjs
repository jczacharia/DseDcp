module.exports = [
  {
    context: ['/api'],
    target: process.env['TEST_API_BASE_URL'] || 'http://localhost:5092',
    secure: false,
    changeOrigin: true,
  },
];
