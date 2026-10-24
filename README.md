# LinguaDesk LibreTranslate configuration patch

This is a configuration patch, not a complete application ZIP. It contains the replacement `docker-compose.yml` and `.env.example` for the existing LinguaDesk project.

## Apply
1. Back up your current `docker-compose.yml` and `.env.example`.
2. Copy this `docker-compose.yml` to the project root, next to the existing `frontend/` and `backend/` folders.
3. Copy `.env.example` to the project root, replacing the example only.
4. If you already have a real `.env`, edit that existing file too: set `Translation__Provider=libretranslate` and `Translation__LibreTranslateUrl=http://libretranslate:5000/translate`. Do not overwrite your real `.env` with `.env.example`; keep your current database credentials.
5. From the project root run `docker compose config`, then `docker compose up -d --build`.

## Notes
- `TranslationService.cs` already contains the LibreTranslate client and maps `zh-CN` to LibreTranslate's `zh` code, so no C# source change is needed for the basic connection.
- `LT_LOAD_ONLY` requests the five supported language codes from the app's list. Exact translation direction support depends on available LibreTranslate/Argos models.
- `LT_UPDATE_MODELS=true` can make first startup take longer. Model data is persisted in the `libretranslate_models` Docker volume.
- The Compose healthcheck uses the official LibreTranslate image's healthcheck script.
- The host port 5000 is published for local testing only; do not expose it publicly without appropriate access controls.
