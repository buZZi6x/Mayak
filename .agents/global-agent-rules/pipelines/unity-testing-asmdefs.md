---
title: Unity Testing And Assembly Definitions
status: active
verified_against: Unity 6000.3.6f1, com.unity.test-framework 1.6.0
verified_on: 2026-08-21
---

# Тесты и assembly definitions

Этот файл дополняет `AGENTS.md` § Unity Testing Policy и § Assembly Definitions
фактической раскладкой проекта. Прогон тестов описан в `unity-cli.md`.

## Assembly-граф

Для нового небольшого Unity-проекта — не больше четырёх сборок. Сборка на фичу
не заводится. Существующий граф не переписывается без измеренной причины по
времени компиляции или по границе зависимостей.

| Сборка | Путь | Роль |
|---|---|---|
| `Game.Runtime` | `Assets/Game/Runtime/` | Игровой рантайм-код |
| `Game.Editor` | `Assets/Game/Editor/` | Editor-only инструменты, `includePlatforms: ["Editor"]` |
| `Game.Tests.EditMode` | `Assets/Tests/EditMode/` | Детерминированная логика, `includePlatforms: ["Editor"]` |
| `Game.Tests.PlayMode` | `Assets/Tests/PlayMode/` | Интеграции с Unity-жизненным циклом |

Код шаблона (`Assets/TutorialInfo`) остаётся в `Assembly-CSharp` и в граф не
входит. Это осознанно: он не наш и подлежит удалению отдельной задачей.

Сборки Photon (`Assets/Photon/**`) — сторонний код, свои asmdef, не трогаем.

### Форма тестового asmdef

Тестовые сборки требуют явных ссылок на раннер и NUnit — Test Framework не
подтягивается автоматически:

```json
{
  "name": "Game.Tests.EditMode",
  "rootNamespace": "Game.Tests.EditMode",
  "references": ["UnityEngine.TestRunner", "UnityEditor.TestRunner", "Game.Runtime"],
  "includePlatforms": ["Editor"],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

`Game.Tests.PlayMode` — то же самое, но без `includePlatforms` и без
`UnityEditor.TestRunner`.

Ключевые моменты, на которых спотыкаются:

- без `overrideReferences: true` + `precompiledReferences` NUnit не виден;
- без `defineConstraints: ["UNITY_INCLUDE_TESTS"]` тестовая сборка попадёт в
  плеерную сборку;
- `autoReferenced: false` — на тесты никто не должен ссылаться.

### Пустой asmdef даёт предупреждение

Пока в папке сборки нет ни одного `.cs`, Unity пишет «Assembly for Assembly
Definition File … will not be compiled, because it has no scripts associated
with it». Это ожидаемо и гаснет само с первым скриптом. Ошибкой не является.

## Что покрывать EditMode

Детерминированная логика, которую можно посчитать без жизненного цикла Unity:

- переходы состояний;
- расчёты и правила;
- инвентарь и экономика;
- генерация уровней (детерминированная часть, с фиксированным seed);
- сериализация сейвов и миграции.

Держи игровые правила тестируемыми вне `MonoBehaviour`, когда это практично:
plain C# класс с правилами + тонкий `MonoBehaviour`-адаптер вокруг него.

## Что покрывать PlayMode

Критичные интеграции с Unity:

- жизненный цикл;
- привязка сцен и префабов;
- ввод;
- физика и столкновения — для этого проекта это ядро, а не периферия;
- потоки UI;
- spawn/despawn;
- переходы между сценами;
- ценные сквозные геймплейные пути.

Для сетевой части (Fusion 2 Shared Mode) отдельно покрывать передачу State
Authority на предмет при захвате и возврат при отпускании.

## Чего не делать

- Не писать PlayMode-тест на тривиальный геттер или на каждый компонент.
- Не выдавать скриншот за доказательство интерактивного поведения.
- Не считать, что тесты закрывают game feel, тайминги анимаций, визуальное
  качество и тюнинг — это по-прежнему ручной плейтест человека.

## Когда проверка не выполнилась

Помечай `degraded`, называй точную причину и перечисляй оставшуюся ручную
проверку. Не выдавай пропущенную проверку за пройденную.

Пример формулировки:

```text
degraded: PlayMode-тесты не запускались — Fusion не сконфигурирован,
App Id не задан.
Ручная проверка: открыть SampleScene, войти в Play Mode, убедиться, что
в Console нет новых ошибок.
```
