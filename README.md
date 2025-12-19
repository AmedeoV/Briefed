# Briefed

RSS feed reader with AI-powered article summaries using Groq and Ollama.

## Features

- 📰 RSS feed management with OPML import/export
- 🤖 AI summarization (Groq + Ollama fallback)
- 💾 Save articles for offline reading
- 🔄 Automatic feed updates
- 👤 User authentication
- 📱 Responsive design

## Tech Stack

ASP.NET Core 9.0 • PostgreSQL • Hangfire • Groq API • Ollama • Entity Framework Core

## Quick Start

1. **Clone and configure:**
   ```bash
   git clone <repo-url>
   cd Briefed
   cp .env.example .env
   # Edit .env with your API keys
   ```

2. **Get API keys:**
   - Groq: [console.groq.com](https://console.groq.com/) (14,400 requests/day free)
   - NewsAPI: [newsapi.org](https://newsapi.org/)

3. **Start services:**
   ```bash
   docker-compose up -d
   ```

4. **Run migrations:**
   ```bash
   cd src/Briefed.Web
   dotnet ef database update --project ../Briefed.Infrastructure
   ```

5. **Launch app:**
   ```bash
   dotnet run
   # or with hot reload: dotnet watch run
   ```

6. Navigate to `https://localhost:5001`

## Configuration

Key settings in `.env`:
- `GROQ_API_KEY` - Groq API key for fast AI summaries
- `NEWSAPI_KEY` - NewsAPI key for trending articles
- `OLLAMA_BASE_URL` - Ollama endpoint for local fallback (optional)

See [docs/GROQ-SETUP.md](docs/GROQ-SETUP.md) for detailed setup.

## Development

**Add migration:**
```bash
dotnet ef migrations add MigrationName --project ../Briefed.Infrastructure
dotnet ef database update --project ../Briefed.Infrastructure
```

**Hot reload:**
```bash
dotnet watch run
```

## License

MIT
