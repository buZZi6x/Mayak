---
title: Unity CLI Pipeline
status: active
verified_against: unity CLI 1.0.0-beta.5 (Windows, MSIX via winget)
verified_on: 2026-08-21
---

# Unity CLI как транспорт автоматизации

`AGENTS.md` § Unity Rules требует использовать официальный Unity CLI как основной
транспорт автоматизации. Этот файл описывает фактическое поведение установленного
CLI, а не документацию: все команды и флаги проверены через `--help` на машине.

CLI имеет статус **experimental / beta**. Перед тем как полагаться на незнакомый
флаг, выполни `unity <command> --help` и убей сомнение фактом.

## Discovery

```bash
unity --version          # 1.0.0-beta.5
unity doctor             # окружение: платформа, installPath, логи, auth, editors, proxy
unity editors -i         # установленные редакторы
unity env                # пути Hub
```

`unity doctor` — первая команда при любом расследовании. Она печатает
`check.binary-on-path`, `check.path-duplicates`, `check.windows-long-paths`,
состояние авторизации, список редакторов и последние строки лога.

### Установка и обновление

| Платформа | Установка | Обновление |
|---|---|---|
| Windows | `winget install Unity.CLI` | `unity upgrade` (MSIX обновляет себя) |
| macOS / Linux | `brew install --cask unity-cli` | `brew upgrade unity-cli` |
| Любая | install.sh с CDN при `UNITY_CLI_CHANNEL=beta` | `unity upgrade` |

На Windows есть и PowerShell-скрипт `install.ps1`, он ставит в
`%LOCALAPPDATA%\Unity\bin` без прав администратора. Канал по умолчанию у скрипта
не опубликован — задавай `UNITY_CLI_CHANNEL=beta`.

## Preflight перед любой автоматизацией

1. `unity doctor` — CLI на месте, `auth.loggedIn true`, нужный редактор в списке.
2. Версия редактора совпадает с `ProjectSettings/ProjectVersion.txt`.
3. **Всегда передавай путь проекта явно.** Без него CLI берёт текущую директорию,
   и команда молча уедет в чужой проект. Это реальная ошибка, а не гипотетическая.
4. Проверь `git status --short` — не затирай чужую незакоммиченную работу.

## Команды проекта

### Открыть проект

```bash
unity open <project-path>
```

Флаги: `--editor-version`, `-e/--editor-path`, `--build-target`, `--args`.

### Тесты

```bash
unity test <project-path> --mode EditMode --output <project-path>/Logs/test-editmode.xml
unity test <project-path> --mode PlayMode --output <project-path>/Logs/test-playmode.xml
```

| Флаг | Назначение |
|---|---|
| `--mode <EditMode или PlayMode>` | Платформа тестов; без флага берётся дефолт редактора |
| `--filter <pattern>` | Только тесты, чьи имена подходят под фильтр |
| `--output <path>` | Отчёт, по умолчанию `test-results.xml` (NUnit XML) |
| `--report-format <nunit, junit, both>` | Формат отчёта |
| `--junit-output <path>` | Путь JUnit-отчёта при `both` |
| `--coverage` | Покрытие; требует пакет Code Coverage в проекте |
| `--timeout <seconds>` | Убить процесс Unity по таймауту |
| `--allow-install` | Доустановить нужную версию редактора |

Отчёт — доказательство прогона. Не заявляй, что тесты прошли, без ссылки на XML.

### Сборка

```bash
unity build <project-path> --target StandaloneWindows64 -o <project-path>/Build/Win64/Game.exe
```

| Флаг | Назначение |
|---|---|
| `--target <target>` | `StandaloneWindows64`, `Android`, `iOS`, `WebGL`; обязателен без `--profile` |
| `--profile <name или path>` | Build profile из `Assets/Settings/Build Profiles` (Unity 6+); сам задаёт target |
| `--execute-method <Type.Method>` | Свой статический метод сборки; без него используется встроенная сборка Unity |
| `-o, --output-path <path>` | Путь результата; обязателен для встроенной сборки |
| `-l, --log-file <path>` | По умолчанию `<project>/Logs/build-<target>-<timestamp>.log` |
| `--no-tail` | Не стримить лог в stdout |
| `--args <string>` | Дополнительные аргументы редактору |

Android-подпись передаётся через `--android-keystore-base64` и
`--android-keystore-password`. **Секреты в аргументах утекают в историю оболочки и
логи CI** — сам CLI об этом предупреждает. В этом проекте не используем.

### Batch-режим и headless-команды

```bash
unity run <project-path> --command <name> -- <args>
unity run <project-path> --timeout 600
```

`--command` выполняет команду, зарегистрированную пакетом `com.unity.pipeline`.
Аргументы после `--` разбираются по схеме команды, а не пробрасываются в Unity.

**У `unity run` нет флага `--args`.** Проброс произвольных аргументов редактору
есть только у `open` и `build`. Проверено 2026-08-21: `unity run … --args`
отвечает `error: unknown option '--args'`.

### Импорт .unitypackage

Штатного headless-импорта у CLI нет. Два рабочих пути:

```bash
unity open <project-path> --args "-importPackage <path.unitypackage>"
```

поднимает GUI-редактор — дорого по памяти и требует присутствия человека.

Детерминированная альтернатива — распаковать пакет самому. `.unitypackage` это
gzip-tar, где каждый ассет представлен группой записей:

```text
<guid>/pathname     путь вида Assets/... , куда файл должен лечь
<guid>/asset        байты файла (у папок отсутствует)
<guid>/asset.meta   .meta с GUID
```

Разложить эти файлы по путям из `pathname` — ровно то, что делает импортёр
Unity; при следующем открытии проекта они импортируются штатно.

**Грабля:** упаковщики различаются. Одни пишут `<guid>/pathname`, другие
`./<guid>/pathname`. Группировать надо по родительскому пути записи целиком, а
не по первому сегменту — иначе при втором формате всё схлопнется в одну группу и
распакуется один файл вместо двухсот. Наступал 2026-08-21 на Fusion 2.0.12.

### Живой редактор

```bash
unity status                       # порт, проект, версия, PID, состояние
unity status --project-path <substr>
unity list                         # инструменты, которые редактор отдаёт агенту
unity command                      # список команд редактора
unity command <name> [args...]
unity command <name> --detach      # как отсоединённая job
unity job                          # управление отсоединёнными job
```

Работает только когда проект открыт в редакторе и в нём стоит `com.unity.pipeline`.

### Пакет Pipeline

```bash
unity pipeline install --project-path <project-path>
unity pipeline upgrade  --project-path <project-path>
unity pipeline list             # все редакторы и статус пакета; --project-path НЕ поддерживает
unity pipeline list-versions
```

## MCP-мост для агента

```bash
unity mcp configure --list          # список клиентов
unity mcp configure claude-code     # делегирует в claude mcp add --scope user
unity mcp configure <client> --dry-run
unity mcp configure <client> --project-path <path>   # прибить запись к проекту
```

Без `--project-path` сервер сам находит подключённый редактор — это правильный
выбор, когда на машине несколько Unity-проектов. С `--project-path` запись
жёстко привязана к одному проекту.

Есть также `unity skill install claude-code` — кладёт агентский скилл Unity CLI
в `~/.claude/skills/unity-cli/`.

## Форматы вывода

`human` в интерактивном терминале, `tsv` при перенаправлении, плюс явные
`--format json` / `--json` / `--format ndjson`. Для парсинга в скриптах —
только `--json` или `--format tsv`; на `human` не опирайся, он меняется.

## Коды выхода

| Код | Значение |
|---|---|
| 0 | Успех |
| 1 | Общая ошибка — смотри stderr |
| 2 | Ошибка использования: неверные флаги или отсутствует обязательный аргумент |
| 3 | Провал аутентификации или авторизации |
| 4 | Нужна конфигурация: аргументы верны, но не хватает настройки или контекста |
| 6 | Основная операция провалилась (сборка упала, тесты не прошли) — терминально |
| 7 | Сервис Unity недоступен — **безопасно ретраить** |
| 130 | Прервано Ctrl+C |
| 143 | Завершено SIGTERM (например, таймаут раннера CI) |

Различай 6 и 7: 6 — это факт провала, его надо чинить; 7 — сеть, его надо
повторить.

## Логи

| Платформа | Путь |
|---|---|
| Windows | `%UserProfile%\AppData\Roaming\UnityHub\logs` |
| macOS | `~/Library/Application Support/UnityHub/logs` |
| Linux | `~/.config/UnityHub/logs` |

Лог самого редактора на Windows: `%LOCALAPPDATA%\Unity\Editor\Editor.log`. Именно
в нём видны ошибки компиляции и исключения пакетов — `unity test` их в stdout не
печатает.

```bash
unity logs                # читать и следить за логом Hub
unity diagnose update     # что CLI разрешил и чем обновляться
```

## Переменные окружения

`UNITY_FORMAT`, `UNITY_QUIET`, `UNITY_NO_BANNER`, `UNITY_NON_INTERACTIVE`,
`UNITY_PROJECT_PATH`, `UNITY_EDITOR_VERSION`, `UNITY_ARCHITECTURE`,
`UNITY_TEST_TIMEOUT`, `UNITY_RUN_TIMEOUT`, `UNITY_PROXY`, `UNITY_NO_ELEVATE`,
`UNITY_SERVICE_ACCOUNT_ID` + `UNITY_SERVICE_ACCOUNT_SECRET` (для CI).

В CI ставь `UNITY_NON_INTERACTIVE=1` и `UNITY_FORMAT=json`.

## Известные подводные камни

- `unity pipeline list` **не принимает** `--project-path`, хотя `install` и
  `upgrade` принимают. Это несимметрично, и это не опечатка.
- Команды без явного пути проекта подхватывают cwd. На машине с несколькими
  Unity-проектами это приводит к работе не над тем проектом.
- `unity license` требует запущенный лицензионный клиент Unity; при закрытом Hub
  падает с «Can't reach the Unity licensing client». Это не признак отсутствия
  лицензии — сборка при этом работает.
- **Нулевой exit code не означает отсутствие ошибок в редакторе.** `unity test`
  вернёт 0, даже если пакет валит исключения при перезагрузке скриптов. Всегда
  сверяй NUnit-отчёт и `Editor.log`.
