# Goblins and Dungeons — Project Agent Rules

Проектные факты и переопределения. Общие правила не дублируются — читай их по
ссылкам.

## Репозиторий

Единственный адрес проекта:

    https://github.com/DREMATeam/Actual

Единственная локальная рабочая копия:

    C:\Users\Vlad\GoblinsDungeons

Все Git-операции и Unity CLI-команды для этого проекта выполняются только с
явно указанным путём к этой рабочей копии.

Исключение для CI, согласованное 2026-10-16: GitHub Actions self-hosted runner
использует изолированную служебную копию в
`C:\Users\Vlad\GoblinsDungeons\.tools\github-actions-runner\_work\Actual\Actual`.
CI не очищает и не переключает основную рабочую копию. Авторские изменения
по-прежнему вносятся только в основной проект; устройство CI — в `.github/CI-CD.md`.

`origin` указывает туда, `main` отслеживает `origin/main`, `remote.pushDefault`
закреплён на `origin`. Пушить куда-либо ещё не нужно и не следует.

Личный `buZZi6x/GoblinsDungeons` был ошибкой и подлежит удалению владельцем.
Если он ещё существует, помни: в его истории лежит Fusion App Id из
`PhotonAppSettings.asset`, и удаление репозитория ключ не отзывает.

## Порядок чтения

1. явная инструкция человека;
2. **этот файл**;
3. `.agents/global-agent-rules/AGENTS.md` — универсальный контракт;
4. `.agents/global-agent-rules/pipelines/unity-cli.md` — транспорт автоматизации;
   `.agents/global-agent-rules/pipelines/unity-testing-asmdefs.md` — тесты и сборки;
   `.agents/global-agent-rules/unity-ui-toolkit-interface-pipeline.md` — runtime UI;
   `.agents/global-agent-rules/code-style.md` — стиль C#;
5. дефолты инструментов.

`.agents/global-agent-rules/` — рабочий источник правды для этого проекта.
Канонический `F:\VIBECODE\_meta\global-agent-rules` на машине разработки
отсутствует, копия на `C:\Users\Vlad\Desktop\agent` неполная. Подробности в
`.agents/global-agent-rules/README.md`.

## Стек

| Что | Значение |
|---|---|
| Путь | `C:\Users\Vlad\GoblinsDungeons` |
| Репозиторий | `github.com/DREMATeam/Actual` |
| Движок | Unity **6000.3.6f1** (`ProjectSettings/ProjectVersion.txt`) |
| Render pipeline | URP 17.3.0 |
| Ввод | Input System 1.18.0 (`Assets/InputSystem_Actions.inputactions`) |
| UI | UI Toolkit (`com.unity.modules.uielements`) + uGUI 2.0.0 |
| Тесты | Test Framework 1.6.0 |
| Автоматизация | `com.unity.pipeline` + Unity CLI |
| Навигация | AI Navigation 2.0.9 |
| Сеть | **Photon Fusion 2** 2.0.12, Shared Mode — `Assets/Photon/` |
| Голос | **Photon Voice 2** 2.63, интеграция `FusionVoiceClient` |
| Камера | **FPS** — вид от первого лица |
| Персонаж | **Полный active ragdoll** на `ConfigurableJoint`, 14 костей |

Render pipeline, стек ввода, UI-стек и граф сборок сохраняются. Не подменяй их
под одну новую систему.

## Дизайн-документ

ГДД живёт в Obsidian, **вне репозитория**:

```text
C:\Users\Vlad\Documents\Obsidian\Goblins and Dungeons\ГДД.md
```

Он владелец player-facing дизайна. Правила, тюнинг, контролы, названия, UI-копия
и контент не выдумываются агентом — они приходят оттуда или от человека.
**Принятые дизайн-решения записывай прямо в ГДД**, в стиле автора, а не только
в план проекта.

Жанр: кооперативный party-horror на 1–4 игрока. Ядро — физический GRAB в духе
Human Fall Flat, процедурные подземелья из готовых комнат, цели в стиле Among Us.
Дизайн-приоритеты по ГДД: Excitement, Community, Destruction. Мета-прогрессии
нет намеренно.

Этапный чеклист: `gamedesign/DEVELOPMENT_PLAN.md`. Этап закрывается только после
реализации, верификации, документации, блокеров и follow-up.

## Раскладка

```text
Assets/Game/Runtime/      Game.Runtime         игровой рантайм
Assets/Game/Runtime/Character/                 active ragdoll, GRAB, FPS-камера
Assets/Game/Art/          исходники: текстуры, материалы, модели, звук, VFX
Assets/Game/Prefabs/      готовые объекты; Player.prefab — авторский риг
Assets/Game/Editor/       Game.Editor          editor-only инструменты
Assets/Tests/EditMode/    Game.Tests.EditMode  детерминированная логика
Assets/Tests/PlayMode/    Game.Tests.PlayMode  интеграции с Unity
Assets/Photon/            Fusion 2 + Voice 2 + Chat + PUN — сторонний код, не трогаем
Assets/Scenes/            сцены
Assets/Settings/          URP-ассеты и профили
.agents/                  правила агента
gamedesign/               намерение фич и этапный план
```

Куда класть контент — в `Assets/Game/Art/РАСКЛАДКА.txt` и
`Assets/Game/Prefabs/РАСКЛАДКА.txt`. Существующие комнаты и `M_*.mat` не
переезжают: тесты и билдеры читают старые пути.

`Assets/TutorialInfo` и `Assets/Readme.asset` удалены 2026-08-21 — это были
остатки шаблона URP. Восстанавливать не нужно.

Больше четырёх собственных сборок не заводим. Сборка на фичу — нет.

## Создание анимаций — UniMate

Для создания скелетных анимаций по тексту используй **UniMate** как рабочий
инструмент подготовки клипов для персонажей, монстров и подвижных объектов.
Это внешний Python/Blender-пайплайн с экспортом FBX/GLB; в Unity поступают
подготовленные анимационные ассеты.

Источники: [код и установка](https://github.com/Friedrich-M/UniMate),
[подготовка своего рига](https://github.com/Friedrich-M/UniMate/tree/main/data_process/rig_preprocess),
[веса и условия использования](https://huggingface.co/Linzhan/UniMate).

**Версии и лицензия.** Проверено по исходникам 2026-10-06, commit
`004d787452e0bf8253d669d6e7b26c2064055cad`: рекомендуемые веса
`unimate_uniml3d_f60_v3`, checkpoint `150000`. Код — MIT, эти веса —
CC BY-NC 4.0. Перед использованием проверяй условия конкретных весов и
исходного ассета; коммерческое применение требует соответствующих прав.
Выбор инструмента не означает, что коммерческие права на веса или результаты
уже получены. Локальная установка и генерация на наших ригах пока не проверены.

Порядок работы:

1. **Задача клипа.** Возьми действие, характер движения и игровой тайминг из
   ГДД или задания человека. Определи длительность, цикл, движение на месте
   либо root motion и назначение клипа. Не придумывай новые способности.
2. **Входной ассет.** Работай с копией ригованного FBX/GLB/glTF с одной
   арматурой. Проверь rest pose, skin weights, имена костей и направление
   вперёд. UniMate подготавливает существующий риг; модель и скиннинг нужны
   до генерации. У v3 предел 71 сустав после подготовки (обучение на 5–70);
   более крупный риг упрощай на копии с сохранением соответствия исходнику.
3. **Окружение.** Инструмент и веса держи в исключённой из Git папке
   `C:\Users\Vlad\GoblinsDungeons\.tools\UniMate`, вне `Assets`.
   Используй отдельное окружение `unimate` и зависимости из закреплённой
   версии upstream: Python 3.10, PyTorch/CUDA 12.4, `bpy` 4.0.
   Проверь совместимость GPU и импорт зависимостей до генерации. Для своего
   рига достаточно весов, `config.json` и `dataset_stats.npy`; полный датасет
   и обучение с нуля не требуются.
4. **Подготовка рига.** Запусти `data_process.rig_preprocess` с
   `--annotate rule`, проверь `REVIEW.md`, `annotation.json` и
   `annotation_preview.png`: все подписи костей, стороны тела и facing pair.
   Исправь разметку и продолжи через `--annotation`. Проверку выполняет агент;
   вопрос человеку нужен только при неясном замысле. LLM-разметка опциональна.
   Затем проверь `summary.json`, `preview.png`, число и порядок суставов.
5. **Генерация.** Используй `unimate.inference.sample`, короткий английский
   запрос об одном движении в стиле `An object walks forward.` и несколько
   вариантов (`--num_repetitions 3`). Запиши prompt, seed, CFG, версию кода
   и checkpoint. Обычный фрагмент v3 — 60 кадров при 30 FPS; для продолжения
   используй motion expansion, для переходов — in-betweening, для изменения
   части тела с сохранением выбранных костей — motion editing.
6. **Экспорт и доработка.** Проверь соответствие motion, cond и canonical
   mesh через `data_process.mesh_animation.sample_manifest`; ошибки порядка
   суставов исправляй до экспорта. Экспортируй GLB/FBX штатным
   `data_process.mesh_animation.animate_motion` или
   `bash scripts/run_animate_motion.sh <samples_dir> <output_dir>`.
   В Bash используй ту же среду Python и совместимые с ней пути.
   В Blender проверь масштаб, оси, деформации, контакты, скольжение ног,
   проникновения в пол, root motion, стык цикла и игровой тайминг; исправь
   клип и сохрани редактируемый `.blend` и итоговый `.fbx`.
7. **Unity.** Импортируй клип с подходящим типом рига и проверь его на целевом
   префабе в Play Mode: скорость, переходы, контакты и взаимодействие с
   коллайдерами. Для игрока сохраняй active ragdoll, GRAB и управление
   State Authority: `GoblinVisualRig` переносит физическую позу в `LateUpdate`.
   Клип нельзя просто назначить Animator на те же кости; интеграция с
   физическими суставами требует отдельной задачи. Риг-билдер ради импорта
   анимации не запускай.
8. **Приёмка и передача.** Исходники, разметку, `cond.npy`, canonical mesh,
   `manifest.json`, варианты и превью сохраняй в
   `gamedesign/Art/Animations/<Asset>/<Clip>/`. Итоговые игровые клипы — в
   `Assets/Game/Art/Animations/{Characters,Enemies,Props}/<Asset>/` по
   `Assets/Game/Art/РАСКЛАДКА.txt`. В карточке ассета сохрани параметры,
   сведения о правах и результаты проверки. Передай пути к `.blend`, `.fbx`,
   клипу и целевому префабу; отличай экспорт от принятой игровой анимации.
   Визуальное качество и ощущение движения подтверждаются просмотром и
   плейтестом; непроведённые проверки перечисляй явно.

Минимальный запуск для своего рига в PowerShell из активного окружения
`unimate`; значения в `<...>` заменяются путями конкретной задачи. После
каждой команды проверяй exit code и созданные файлы. Инструмент и выбранные
веса должны быть установлены по upstream-инструкции до этих команд:

```powershell
$unimateRoot = 'C:\Users\Vlad\GoblinsDungeons\.tools\UniMate'
$animationInput = '<абсолютный путь к копии ригованного FBX/GLB/glTF>'
$animationWork = 'C:\Users\Vlad\GoblinsDungeons\gamedesign\Art\Animations\<Asset>\<Clip>'
$animationModel = Join-Path $unimateRoot 'outputs\unimate_uniml3d_f60_v3'
Set-Location -LiteralPath $unimateRoot
python -m data_process.rig_preprocess run --input "$animationInput" --output_dir "$animationWork\rig" --annotate rule --exp_dir "$animationModel"
```

После проверки и исправления `annotation.json` продолжи в том же окружении:

```powershell
python -m data_process.rig_preprocess run --input "$animationInput" --output_dir "$animationWork\rig" --annotation "$animationWork\rig\annotation.json" --exp_dir "$animationModel"
python -m unimate.inference.sample --exp_dir "$animationModel" --asset "$animationWork\rig" --prompt "An object walks forward." --num_repetitions 3 --seed 10 --cfg_scale 3 --output_dir "$animationWork\samples"
python -m data_process.mesh_animation.sample_manifest "$animationWork\samples"
```

Последняя команда выдаёт проверенные задания экспорта: `anim_path`,
`char_path`, `cond_path`, `dataset_type` через табуляцию. Для каждого задания
передай эти значения без подмены исходным ригом:

```powershell
python -m data_process.mesh_animation.animate_motion --anim_path '<anim_path>' --char_path '<char_path>' --cond_path '<cond_path>' --dataset_type '<dataset_type>' --output_dir "$animationWork\animated"
```

## Запуск и проверка

Транспорт — Unity CLI. Полный справочник в
`.agents/global-agent-rules/pipelines/unity-cli.md`.

```bash
unity doctor
unity open  "C:\Users\Vlad\GoblinsDungeons"
unity test  "C:\Users\Vlad\GoblinsDungeons" --mode EditMode --output "C:\Users\Vlad\GoblinsDungeons\Logs\test-editmode.xml"
unity build "C:\Users\Vlad\GoblinsDungeons" --target StandaloneWindows64 -o "C:\Users\Vlad\GoblinsDungeons\Build\Win64\GoblinsDungeons.exe"
unity test  "C:\Users\Vlad\GoblinsDungeons" --mode PlayMode --output "C:\Users\Vlad\GoblinsDungeons\Logs\test-playmode.xml"
unity status
```

**`unity run --command` не запускает пункты меню** — он знает только команды
пакета `com.unity.pipeline`. Для editor-билдеров используй штатный Unity:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -quit \
  -projectPath "C:\Users\Vlad\GoblinsDungeons" \
  -executeMethod Game.Editor.CharacterRigBuilder.BuildAll \
  -logFile "C:\Users\Vlad\GoblinsDungeons\Logs\build-rig.log"
```

Пересборка рига **перезаписывает** `Player.prefab` и `Playground.unity`. Ручные
правки в них пропадут — не запускай билдер, не спросив.

MCP-мост в Claude Code настроен как `unity-editor-mcp` (scope user, без привязки
к проекту — находит подключённый редактор сам). Через него доступны Console,
инспекция сцен и команды редактора, когда проект открыт и пакет
`com.unity.pipeline` скомпилирован.

## Ловушки

1. **Всегда передавай путь проекта явно.** Unity CLI без пути берёт cwd, а на
   машине лежат ещё `TamplierSpellLab` и `30Min`. `unity pipeline list` уже
   один раз показал чужой проект.
2. **Нулевой exit code не означает, что всё чисто.** `unity test` вернёт 0, даже
   если пакет валит исключения при перезагрузке скриптов. Всегда сверяй
   NUnit-отчёт и `%LOCALAPPDATA%\Unity\Editor\Editor.log`. На этом уже один раз
   попался сломанный Fusion 2.0.4.
3. `unity pipeline list` не принимает `--project-path`, хотя `install` и
   `upgrade` принимают.
4. `unity license` падает с «Can't reach the Unity licensing client» при закрытом
   Hub. На сборку это не влияет.
5. `doc.photonengine.com` из этой сети не отвечает. Документацию Photon искать
   через доступные зеркала или VPN.
6. **CLI `gh` не установлен.** На PATH есть `AppData\Local\GitHubDesktop\bin`,
   но это GitHub Desktop (`github.bat`), а не GitHub CLI. Ставится через
   `winget install GitHub.cli`.
7. `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` содержит App Id и
   **коммитится намеренно** — репозиторий приватный. Не добавлять в
   `.gitignore`. Пересмотреть, если репозиторий станет публичным.

## Границы

- Не коммить и не пушь без прямого разрешения.
- Не трогай `Assets/Settings/*`, `SampleScene` и
  `InputSystem_Actions.inputactions` без задачи, которая ими владеет.
- Не трогай `Assets/Photon/**` — сторонний SDK. Обновляется заменой пакета
  целиком, а не правкой файлов. Вместе с Voice туда приехал PUN 2, который мы не
  используем; вырезать его самостоятельно не надо, это открытое решение.
- Не выбирай целевую платформу — это открытое решение человека, оно перечислено
  в `gamedesign/DEVELOPMENT_PLAN.md`.
- **Камера — FPS.** Параллельный TPS-режим в MVP не закладываем.
- **GRAB проектируется под передачу State Authority** с самого начала, даже пока
  код однопользовательский. Это следствие выбора Fusion 2 Shared Mode.
- **Мокап HUD запрашивается у человека в момент старта S6, а не заранее.** До
  этого момента вопрос не поднимается и этап не считается заблокированным.
