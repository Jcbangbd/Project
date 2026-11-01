import argparse
import asyncio
import importlib.util
import inspect
import json
import sys
from pathlib import Path
from typing import Any, Callable, Dict


def _load_schedule(
    algorithm_path: Path,
) -> Callable[..., Any]:
    module_name = "installed_scheduling_algorithm"
    specification = importlib.util.spec_from_file_location(
        module_name,
        str(algorithm_path),
    )
    if (
        specification is None
        or specification.loader is None
    ):
        raise RuntimeError(
            "Cannot load the installed algorithm module."
        )

    module = importlib.util.module_from_spec(specification)

    # 必须先注册再执行。
    # dataclass、类型注解等功能会在模块执行阶段，
    # 通过 cls.__module__ 查询 sys.modules。
    sys.modules[module_name] = module
    try:
        specification.loader.exec_module(module)
    except Exception:
        # 导入失败时清理半初始化模块，避免后续误用。
        sys.modules.pop(module_name, None)
        raise

    schedule = getattr(module, "schedule", None)
    if not callable(schedule):
        raise RuntimeError(
            "The installed algorithm has no callable "
            "schedule(problem) function."
        )
    return schedule


def _execute(
    schedule: Callable[..., Any],
    problem: Dict[str, Any],
) -> Any:
    result = schedule(problem)
    if inspect.isawaitable(result):
        result = asyncio.run(result)
    if not isinstance(result, dict):
        raise RuntimeError(
            "schedule(problem) must return a JSON object/dict."
        )
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--algorithm", required=True)
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    algorithm_path = Path(args.algorithm).resolve()
    input_path = Path(args.input).resolve()
    output_path = Path(args.output).resolve()

    problem = json.loads(
        input_path.read_text(encoding="utf-8")
    )
    if not isinstance(problem, dict):
        raise RuntimeError(
            "Algorithm input must be a JSON object."
        )

    schedule = _load_schedule(algorithm_path)
    result = _execute(schedule, problem)
    output_path.write_text(
        json.dumps(result, ensure_ascii=False),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
