"""
双 RMG 轨道吊调度算法：FastAPI/Unity JSON 适配版。

本文件只依赖 Python 标准库。现有本地算法服务会动态加载本文件，并调用：

    schedule(problem: dict) -> dict

输入 problem 来自 Unity 的 TaskCaseDocumentDto，输出与
Unity 的 SchedulingResultDto 字段一致。
"""

from __future__ import annotations

import math
import random
import time
from dataclasses import dataclass
from typing import Any, Dict, List, Optional, Sequence, Set, Tuple


# 堆场与设备参数
NUM_BAYS = 50
NUM_ROWS = 10
NUM_LAYERS = 5
BAY_PITCH_M = 6.10
ROW_PITCH_M = 2.50
LAYER_HEIGHT_M = 2.90
V_MOVE_MPS = 2.0
V_ROW_MPS = 0.8
V_LIFT_MPS = 0.725
SEA_ZONE_MAX = 15.0
LAND_ZONE_MIN = 35.0

START_POS = {
    1: (1.0, 1.0),
    2: (50.0, 10.0),
}

BASE_TIME = {
    "卸船": 20.0,
    "装船": 20.0,
    "翻箱": 25.0,
    "卸车": 20.0,
    "装车": 20.0,
}

EXPECTED_ORIGIN_KIND = {
    "卸船": "海侧支架",
    "装船": "堆场",
    "翻箱": "堆场",
    "卸车": "陆侧车道",
    "装车": "堆场",
}

DESTINATION_KIND = {
    "卸船": "堆场",
    "装船": "海侧支架",
    "翻箱": "堆场",
    "卸车": "堆场",
    "装车": "陆侧车道",
}

# 与用户提供的遗传算法 v2 保持一致
POPULATION_SIZE = 160
MAX_GENERATIONS = 300
ELITE_COUNT = 8
TOURNAMENT_SIZE = 4
CROSSOVER_RATE = 0.90
MUTATION_RATE = 0.20
ASSIGNMENT_MUTATION_RATE = 0.08
STAGNATION_LIMIT = 80
GA_RANDOM_SEED = 42


@dataclass
class Individual:
    order: List[str]
    flex_owner: Dict[str, int]
    score: float = float("inf")

    def clone(self) -> "Individual":
        return Individual(
            self.order.copy(),
            self.flex_owner.copy(),
            self.score,
        )


def _text(value: Any, default: str = "") -> str:
    if value is None:
        return default
    result = str(value).strip()
    return result if result else default


def _number(
    value: Any,
    field_name: str,
    default: Optional[float] = None,
) -> float:
    if value is None or value == "":
        if default is not None:
            return default
        raise ValueError(f"{field_name} 不能为空。")

    try:
        result = float(value)
    except (TypeError, ValueError) as exc:
        raise ValueError(
            f"{field_name} 必须是数字，实际值={value!r}。"
        ) from exc

    if not math.isfinite(result):
        raise ValueError(f"{field_name} 必须是有限数字。")
    return result


def _integer(
    value: Any,
    field_name: str,
    default: int = 1,
) -> int:
    number = _number(value, field_name, float(default))
    rounded = int(round(number))
    if abs(number - rounded) > 1e-6:
        raise ValueError(f"{field_name} 必须是整数，实际值={value!r}。")
    return rounded


def _normalize_task_type(value: Any) -> str:
    text = (
        _text(value)
        .replace(" ", "")
        .replace("→", "->")
        .replace("－", "-")
    )

    route_map = (
        ("支架->堆场", "卸船"),
        ("海侧->堆场", "卸船"),
        ("堆场->支架", "装船"),
        ("堆场->海侧", "装船"),
        ("堆场->堆场", "翻箱"),
        ("车道->堆场", "卸车"),
        ("陆侧->堆场", "卸车"),
        ("堆场->车道", "装车"),
        ("堆场->陆侧", "装车"),
    )
    for route, normalized in route_map:
        if route in text:
            return normalized

    for normalized in BASE_TIME:
        if normalized in text:
            return normalized

    raise ValueError(f"无法识别任务类型：{value!r}。")


def _position_value(
    job: Dict[str, Any],
    direct_name: str,
    position: Dict[str, Any],
    nested_name: str,
    field_name: str,
    default: Optional[float] = None,
) -> float:
    value = job.get(direct_name)
    if value is None or value == "":
        value = position.get(nested_name)
    return _number(value, field_name, default)


def _build_tasks(
    problem: Dict[str, Any],
) -> Dict[str, Dict[str, Any]]:
    jobs = problem.get("jobs")
    if not isinstance(jobs, list) or not jobs:
        raise ValueError("算法输入缺少非空 jobs 数组。")

    tasks: Dict[str, Dict[str, Any]] = {}
    for index, job in enumerate(jobs, start=1):
        if not isinstance(job, dict):
            raise ValueError(f"jobs[{index - 1}] 不是 JSON 对象。")

        job_id = _text(job.get("jobId"), str(index))
        if job_id in tasks:
            raise ValueError(f"jobId 重复：{job_id}。")

        source = job.get("source") or {}
        target = job.get("target") or {}
        if not isinstance(source, dict) or not isinstance(target, dict):
            raise ValueError(
                f"作业 {job_id} 的 source/target 必须是 JSON 对象。"
            )

        task_type = _normalize_task_type(job.get("taskType"))
        origin_bay = _position_value(
            job,
            "startPointPosition",
            source,
            "bay",
            f"作业 {job_id} 起点贝位",
        )
        origin_row = _position_value(
            job,
            "startList",
            source,
            "row",
            f"作业 {job_id} 起点列",
            1.0,
        )
        destination_bay = _position_value(
            job,
            "endPointPosition",
            target,
            "bay",
            f"作业 {job_id} 终点贝位",
        )
        destination_row = _position_value(
            job,
            "endList",
            target,
            "row",
            f"作业 {job_id} 终点列",
            1.0,
        )
        origin_layer = _integer(
            source.get("tier"),
            f"作业 {job_id} 起点层",
            1,
        )
        destination_layer = _integer(
            target.get("tier"),
            f"作业 {job_id} 终点层",
            1,
        )

        if not 1 <= origin_layer <= NUM_LAYERS:
            raise ValueError(
                f"作业 {job_id} 起点层必须在 1～{NUM_LAYERS}。"
            )
        if not 1 <= destination_layer <= NUM_LAYERS:
            raise ValueError(
                f"作业 {job_id} 终点层必须在 1～{NUM_LAYERS}。"
            )

        tasks[job_id] = {
            "job_id": job_id,
            "container_no": _text(
                job.get("containerNo"),
                f"TASK-{job_id}",
            ),
            "type": task_type,
            "crane_hint": _text(job.get("craneHint")),
            "O_kind": EXPECTED_ORIGIN_KIND[task_type],
            "O": origin_bay,
            "O_row": origin_row,
            "O_layer": origin_layer,
            "D_kind": DESTINATION_KIND[task_type],
            "D": destination_bay,
            "D_row": destination_row,
            "D_layer": destination_layer,
            "source": source,
            "target": target,
            "release": max(
                0.0,
                _number(
                    job.get("releaseTimeSeconds"),
                    f"作业 {job_id} releaseTimeSeconds",
                    0.0,
                ),
            ),
            "priority": int(
                _number(
                    job.get("priority"),
                    f"作业 {job_id} priority",
                    0.0,
                )
            ),
        }

    return tasks


def _prepare_tasks(
    tasks: Dict[str, Dict[str, Any]],
) -> Tuple[List[str], List[str], List[str]]:
    fixed_1: List[str] = []
    fixed_2: List[str] = []
    flexible: List[str] = []

    for task_id, task in tasks.items():
        horizontal_time = max(
            abs(task["O"] - task["D"]) * BAY_PITCH_M / V_MOVE_MPS,
            abs(task["O_row"] - task["D_row"])
            * ROW_PITCH_M
            / V_ROW_MPS,
        )
        lifting_time = (
            (task["O_layer"] + task["D_layer"])
            * LAYER_HEIGHT_M
            / V_LIFT_MPS
        )
        task["P"] = (
            horizontal_time
            + lifting_time
            + BASE_TIME[task["type"]]
        )
        task["lo"] = min(task["O"], task["D"])
        task["hi"] = max(task["O"], task["D"])

        if task["hi"] <= SEA_ZONE_MAX:
            fixed_1.append(task_id)
        elif task["lo"] >= LAND_ZONE_MIN:
            fixed_2.append(task_id)
        else:
            flexible.append(task_id)

    return fixed_1, fixed_2, flexible


def _build_conflicts(
    tasks: Dict[str, Dict[str, Any]],
    safe_bays: float,
) -> Tuple[Dict[str, Set[str]], List[Tuple[str, str]]]:
    task_ids = list(tasks)
    neighbors: Dict[str, Set[str]] = {
        task_id: set() for task_id in task_ids
    }
    pairs: List[Tuple[str, str]] = []
    safe_distance = max(0.0, safe_bays) * BAY_PITCH_M

    for index, first in enumerate(task_ids):
        for second in task_ids[index + 1 :]:
            first_task = tasks[first]
            second_task = tasks[second]
            first_lo = first_task["lo"] * BAY_PITCH_M
            first_hi = first_task["hi"] * BAY_PITCH_M
            second_lo = second_task["lo"] * BAY_PITCH_M
            second_hi = second_task["hi"] * BAY_PITCH_M
            separated = (
                first_hi + safe_distance <= second_lo
                or second_hi + safe_distance <= first_lo
            )
            if separated:
                continue
            pairs.append((first, second))
            neighbors[first].add(second)
            neighbors[second].add(first)

    return neighbors, pairs


def _travel_time(
    tasks: Dict[str, Dict[str, Any]],
    previous: Optional[str],
    current: str,
    crane: int,
) -> float:
    if previous is None:
        previous_bay, previous_row = START_POS[crane]
    else:
        previous_bay = tasks[previous]["D"]
        previous_row = tasks[previous]["D_row"]

    return max(
        abs(previous_bay - tasks[current]["O"])
        * BAY_PITCH_M
        / V_MOVE_MPS,
        abs(previous_row - tasks[current]["O_row"])
        * ROW_PITCH_M
        / V_ROW_MPS,
    )


def _travel_distance(
    tasks: Dict[str, Dict[str, Any]],
    previous: Optional[str],
    current: str,
    crane: int,
) -> float:
    if previous is None:
        previous_bay, previous_row = START_POS[crane]
    else:
        previous_bay = tasks[previous]["D"]
        previous_row = tasks[previous]["D_row"]

    return max(
        abs(previous_bay - tasks[current]["O"]) * BAY_PITCH_M,
        abs(previous_row - tasks[current]["O_row"]) * ROW_PITCH_M,
    )


def _full_assignment(
    individual: Individual,
    fixed_1: Sequence[str],
    fixed_2: Sequence[str],
) -> Dict[str, int]:
    assignment = {task_id: 1 for task_id in fixed_1}
    assignment.update({task_id: 2 for task_id in fixed_2})
    assignment.update(individual.flex_owner)
    return assignment


def _repair_assignment(
    individual: Individual,
    fixed_1: Sequence[str],
    fixed_2: Sequence[str],
    flexible: Sequence[str],
    rng: random.Random,
) -> bool:
    assignment = _full_assignment(individual, fixed_1, fixed_2)
    counts = {
        1: sum(owner == 1 for owner in assignment.values()),
        2: sum(owner == 2 for owner in assignment.values()),
    }

    for empty_crane in (1, 2):
        if counts[empty_crane] > 0:
            continue
        other = 3 - empty_crane
        candidates = [
            task_id
            for task_id in flexible
            if individual.flex_owner[task_id] == other
            and counts[other] > 1
        ]
        if not candidates:
            return False
        chosen = rng.choice(candidates)
        individual.flex_owner[chosen] = empty_crane
        counts[empty_crane] += 1
        counts[other] -= 1
    return True


def _decode(
    individual: Individual,
    tasks: Dict[str, Dict[str, Any]],
    fixed_1: Sequence[str],
    fixed_2: Sequence[str],
    conflict_neighbors: Dict[str, Set[str]],
) -> Tuple[float, Optional[Dict[str, Any]]]:
    assignment = _full_assignment(individual, fixed_1, fixed_2)
    if set(assignment) != set(tasks):
        return float("inf"), None
    if 1 not in assignment.values() or 2 not in assignment.values():
        return float("inf"), None

    crane_ready = {1: 0.0, 2: 0.0}
    previous_task: Dict[int, Optional[str]] = {1: None, 2: None}
    start_times: Dict[str, float] = {}
    finish_times: Dict[str, float] = {}
    sequences: Dict[int, List[str]] = {1: [], 2: []}

    for task_id in individual.order:
        crane = assignment[task_id]
        arrival = crane_ready[crane] + _travel_time(
            tasks,
            previous_task[crane],
            task_id,
            crane,
        )
        conflict_ready = max(
            (
                finish_times[other]
                for other in conflict_neighbors[task_id]
                if other in finish_times
            ),
            default=0.0,
        )
        start = max(
            arrival,
            conflict_ready,
            tasks[task_id]["release"],
        )
        finish = start + tasks[task_id]["P"]
        start_times[task_id] = start
        finish_times[task_id] = finish
        crane_ready[crane] = finish
        previous_task[crane] = task_id
        sequences[crane].append(task_id)

    result = {
        "assignment": assignment,
        "start": start_times,
        "finish": finish_times,
        "sequences": sequences,
    }
    return max(finish_times.values()), result


def _heuristic_owner(
    task_id: str,
    tasks: Dict[str, Dict[str, Any]],
) -> int:
    distances = {}
    for crane in (1, 2):
        start_bay, start_row = START_POS[crane]
        distances[crane] = max(
            abs(start_bay - tasks[task_id]["O"])
            * BAY_PITCH_M
            / V_MOVE_MPS,
            abs(start_row - tasks[task_id]["O_row"])
            * ROW_PITCH_M
            / V_ROW_MPS,
        )
    return min(distances, key=distances.get)


def _create_individual(
    index: int,
    task_ids: Sequence[str],
    tasks: Dict[str, Dict[str, Any]],
    fixed_1: Sequence[str],
    fixed_2: Sequence[str],
    flexible: Sequence[str],
    rng: random.Random,
) -> Individual:
    if index == 0:
        order = sorted(
            task_ids,
            key=lambda task_id: (
                -tasks[task_id]["priority"],
                tasks[task_id]["release"],
                tasks[task_id]["O"],
                task_id,
            ),
        )
    elif index == 1:
        order = sorted(
            task_ids,
            key=lambda task_id: (
                tasks[task_id]["lo"],
                task_id,
            ),
        )
    elif index == 2:
        order = sorted(
            task_ids,
            key=lambda task_id: (
                -tasks[task_id]["P"],
                task_id,
            ),
        )
    else:
        order = list(task_ids)
        rng.shuffle(order)

    flex_owner = {}
    for task_id in flexible:
        flex_owner[task_id] = (
            _heuristic_owner(task_id, tasks)
            if index < 3
            else rng.choice((1, 2))
        )

    individual = Individual(order, flex_owner)
    _repair_assignment(
        individual,
        fixed_1,
        fixed_2,
        flexible,
        rng,
    )
    return individual


def _order_crossover(
    first_order: Sequence[str],
    second_order: Sequence[str],
    rng: random.Random,
) -> List[str]:
    size = len(first_order)
    if size < 2:
        return list(first_order)

    left, right = sorted(rng.sample(range(size), 2))
    right += 1
    child: List[Optional[str]] = [None] * size
    child[left:right] = first_order[left:right]
    fixed = set(child[left:right])
    fill = [item for item in second_order if item not in fixed]
    fill_index = 0
    for index in list(range(right, size)) + list(range(0, left)):
        child[index] = fill[fill_index]
        fill_index += 1
    return [item for item in child if item is not None]


def _crossover(
    first: Individual,
    second: Individual,
    flexible: Sequence[str],
    rng: random.Random,
) -> Tuple[Individual, Individual]:
    if rng.random() >= CROSSOVER_RATE:
        return first.clone(), second.clone()

    first_child_owner = {}
    second_child_owner = {}
    for task_id in flexible:
        if rng.random() < 0.5:
            first_child_owner[task_id] = first.flex_owner[task_id]
            second_child_owner[task_id] = second.flex_owner[task_id]
        else:
            first_child_owner[task_id] = second.flex_owner[task_id]
            second_child_owner[task_id] = first.flex_owner[task_id]

    return (
        Individual(
            _order_crossover(first.order, second.order, rng),
            first_child_owner,
        ),
        Individual(
            _order_crossover(second.order, first.order, rng),
            second_child_owner,
        ),
    )


def _mutate(
    individual: Individual,
    flexible: Sequence[str],
    rng: random.Random,
) -> None:
    if (
        len(individual.order) >= 2
        and rng.random() < MUTATION_RATE
    ):
        first, second = sorted(
            rng.sample(range(len(individual.order)), 2)
        )
        if rng.random() < 0.5:
            (
                individual.order[first],
                individual.order[second],
            ) = (
                individual.order[second],
                individual.order[first],
            )
        else:
            individual.order[first : second + 1] = reversed(
                individual.order[first : second + 1]
            )

    for task_id in flexible:
        if rng.random() < ASSIGNMENT_MUTATION_RATE:
            individual.flex_owner[task_id] = (
                3 - individual.flex_owner[task_id]
            )
    individual.score = float("inf")


def _tournament_select(
    population: Sequence[Individual],
    rng: random.Random,
) -> Individual:
    competitors = rng.sample(
        list(population),
        min(TOURNAMENT_SIZE, len(population)),
    )
    return min(competitors, key=lambda item: item.score)


def _chromosome_key(
    individual: Individual,
    flexible: Sequence[str],
) -> Tuple[Tuple[str, ...], Tuple[int, ...]]:
    return (
        tuple(individual.order),
        tuple(
            individual.flex_owner[task_id]
            for task_id in flexible
        ),
    )


def _run_genetic_algorithm(
    tasks: Dict[str, Dict[str, Any]],
    fixed_1: Sequence[str],
    fixed_2: Sequence[str],
    flexible: Sequence[str],
    conflict_neighbors: Dict[str, Set[str]],
    deadline: Optional[float],
) -> Tuple[float, Dict[str, Any], int]:
    task_ids = list(tasks)
    if len(task_ids) < 2:
        raise ValueError("双 RMG 调度至少需要两个任务。")
    if (not fixed_1 and not flexible) or (
        not fixed_2 and not flexible
    ):
        raise ValueError(
            "当前任务区域使至少一台 RMG 无法分配任务。"
        )

    rng = random.Random(GA_RANDOM_SEED)
    population = [
        _create_individual(
            index,
            task_ids,
            tasks,
            fixed_1,
            fixed_2,
            flexible,
            rng,
        )
        for index in range(POPULATION_SIZE)
    ]
    evaluation_cache: Dict[
        Tuple[Tuple[str, ...], Tuple[int, ...]],
        float,
    ] = {}

    def evaluate(individual: Individual) -> None:
        key = _chromosome_key(individual, flexible)
        if key not in evaluation_cache:
            score, _ = _decode(
                individual,
                tasks,
                fixed_1,
                fixed_2,
                conflict_neighbors,
            )
            evaluation_cache[key] = score
        individual.score = evaluation_cache[key]

    best: Optional[Individual] = None
    stagnant = 0
    generations_run = 0

    for generation in range(MAX_GENERATIONS + 1):
        for individual in population:
            if individual.score == float("inf"):
                evaluate(individual)

        population.sort(key=lambda item: item.score)
        current = population[0]
        generations_run = generation

        if best is None or current.score < best.score - 1e-9:
            best = current.clone()
            stagnant = 0
        else:
            stagnant += 1

        if (
            generation >= MAX_GENERATIONS
            or stagnant >= STAGNATION_LIMIT
            or (
                deadline is not None
                and time.monotonic() >= deadline
            )
        ):
            break

        next_population = [
            population[index].clone()
            for index in range(
                min(ELITE_COUNT, len(population))
            )
        ]
        while len(next_population) < POPULATION_SIZE:
            first_parent = _tournament_select(population, rng)
            second_parent = _tournament_select(population, rng)
            children = _crossover(
                first_parent,
                second_parent,
                flexible,
                rng,
            )
            for child in children:
                _mutate(child, flexible, rng)
                if not _repair_assignment(
                    child,
                    fixed_1,
                    fixed_2,
                    flexible,
                    rng,
                ):
                    child.score = float("inf")
                if len(next_population) < POPULATION_SIZE:
                    next_population.append(child)
        population = next_population

    if best is None:
        raise RuntimeError("遗传算法没有生成候选解。")

    best_score, best_schedule = _decode(
        best,
        tasks,
        fixed_1,
        fixed_2,
        conflict_neighbors,
    )
    if best_schedule is None or not math.isfinite(best_score):
        raise RuntimeError("遗传算法没有得到可用调度结果。")

    return best_score, best_schedule, generations_run


def _validate_schedule(
    schedule_result: Dict[str, Any],
    tasks: Dict[str, Dict[str, Any]],
    conflict_pairs: Sequence[Tuple[str, str]],
) -> None:
    all_scheduled = (
        schedule_result["sequences"][1]
        + schedule_result["sequences"][2]
    )
    if (
        len(all_scheduled) != len(tasks)
        or set(all_scheduled) != set(tasks)
    ):
        raise RuntimeError("遗传算法结果存在任务遗漏或重复。")

    for crane in (1, 2):
        previous: Optional[str] = None
        for task_id in schedule_result["sequences"][crane]:
            earliest = (
                0.0
                if previous is None
                else schedule_result["finish"][previous]
            ) + _travel_time(
                tasks,
                previous,
                task_id,
                crane,
            )
            earliest = max(earliest, tasks[task_id]["release"])
            if (
                schedule_result["start"][task_id] + 1e-7
                < earliest
            ):
                raise RuntimeError(
                    f"RMG{crane} 任务 {task_id} "
                    "违反时间连续性。"
                )
            previous = task_id

    for first, second in conflict_pairs:
        nonoverlap = (
            schedule_result["finish"][first]
            <= schedule_result["start"][second] + 1e-7
            or schedule_result["finish"][second]
            <= schedule_result["start"][first] + 1e-7
        )
        if not nonoverlap:
            raise RuntimeError(
                f"任务 {first} 与任务 {second} "
                "存在作业时间冲突。"
            )


def _display_code(
    position: Dict[str, Any],
    kind: str,
    bay: float,
    row: float,
    layer: int,
) -> str:
    explicit = _text(position.get("displayCode"))
    if explicit:
        return explicit
    if kind == "陆侧车道":
        return "港外"
    area_id = _text(position.get("areaId"), kind)
    return f"{area_id}-{bay:g}-{row:g}-{layer}"


def _build_output(
    problem: Dict[str, Any],
    tasks: Dict[str, Dict[str, Any]],
    best_score: float,
    schedule_result: Dict[str, Any],
    generations_run: int,
) -> Dict[str, Any]:
    operations: List[Dict[str, Any]] = []
    empty_travel_distance = 0.0

    for crane in (1, 2):
        previous: Optional[str] = None
        for task_id in schedule_result["sequences"][crane]:
            task = tasks[task_id]
            empty_travel_distance += _travel_distance(
                tasks,
                previous,
                task_id,
                crane,
            )

            source = task["source"]
            target = task["target"]
            operations.append(
                {
                    "sequence": 0,
                    "jobId": task["job_id"],
                    "containerNo": task["container_no"],
                    "craneId": f"RMG{crane}",
                    "fromCode": _display_code(
                        source,
                        task["O_kind"],
                        task["O"],
                        task["O_row"],
                        task["O_layer"],
                    ),
                    "toCode": _display_code(
                        target,
                        task["D_kind"],
                        task["D"],
                        task["D_row"],
                        task["D_layer"],
                    ),
                    "plannedStartSeconds": round(
                        schedule_result["start"][task_id],
                        3,
                    ),
                    "plannedEndSeconds": round(
                        schedule_result["finish"][task_id],
                        3,
                    ),
                    "taskType": task["type"],
                    "sourceAreaType": _text(
                        source.get("areaType")
                    ),
                    "sourceAreaId": _text(source.get("areaId")),
                    "sourceBay": task["O"],
                    "sourceRow": task["O_row"],
                    "sourceTier": task["O_layer"],
                    "targetAreaType": _text(
                        target.get("areaType")
                    ),
                    "targetAreaId": _text(target.get("areaId")),
                    "targetBay": task["D"],
                    "targetRow": task["D_row"],
                    "targetTier": task["D_layer"],
                }
            )
            previous = task_id

    operations.sort(
        key=lambda item: (
            item["plannedStartSeconds"],
            item["plannedEndSeconds"],
            item["craneId"],
            item["jobId"],
        )
    )
    for sequence, operation in enumerate(operations, start=1):
        operation["sequence"] = sequence

    task_id = _text(problem.get("taskId"), "scene-task")
    task_version = _integer(
        problem.get("taskVersion"),
        "taskVersion",
        1,
    )
    return {
        "scheduleId": f"{task_id}-v{task_version}-ga",
        "operations": operations,
        "metrics": {
            "jobCount": len(operations),
            "makespanSeconds": round(best_score, 3),
            "emptyTravelDistance": round(
                empty_travel_distance,
                3,
            ),
            "generationsRun": generations_run,
            "algorithm": "genetic-algorithm-v2",
        },
    }


def schedule(problem: Dict[str, Any]) -> Dict[str, Any]:
    """
    FastAPI 本地算法服务调用入口。

    不读取 Excel、不访问其他 HTTP 地址、不写结果文件。
    """
    if not isinstance(problem, dict):
        raise ValueError("problem 必须是 JSON 对象。")

    tasks = _build_tasks(problem)
    fixed_1, fixed_2, flexible = _prepare_tasks(tasks)

    constraints = problem.get("constraints") or {}
    if not isinstance(constraints, dict):
        raise ValueError("constraints 必须是 JSON 对象。")

    safe_bays = _number(
        constraints.get("minimumCraneDistance"),
        "minimumCraneDistance",
        2.0,
    )
    conflict_neighbors, conflict_pairs = _build_conflicts(
        tasks,
        safe_bays,
    )

    maximum_seconds = _number(
        constraints.get("maximumRunSeconds"),
        "maximumRunSeconds",
        600.0,
    )
    # 给 worker 保存 JSON 和 FastAPI 返回响应预留 5 秒。
    deadline = (
        time.monotonic() + max(1.0, maximum_seconds - 5.0)
        if maximum_seconds > 0
        else None
    )

    best_score, schedule_result, generations_run = (
        _run_genetic_algorithm(
            tasks,
            fixed_1,
            fixed_2,
            flexible,
            conflict_neighbors,
            deadline,
        )
    )
    _validate_schedule(
        schedule_result,
        tasks,
        conflict_pairs,
    )
    return _build_output(
        problem,
        tasks,
        best_score,
        schedule_result,
        generations_run,
    )

