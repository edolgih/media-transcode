#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 8 ]]; then
    echo "usage: run-rife-trt.sh <input> <output> <fps-multiplier> <container> <interp-model> <cq> <maxrate-kbps> <bufsize-kbps>" >&2
    exit 2
fi

input_path="$1"
output_path="$2"
fps_multiplier="$3"
container_name="$4"
interp_model="$5"
cq="$6"
maxrate_kbps="$7"
bufsize_kbps="$8"

case "$fps_multiplier" in
    2|3)
        ;;
    *)
        echo "fps-multiplier must be 2 or 3" >&2
        exit 2
        ;;
esac

case "$interp_model" in
    4.25.lite|4.25|4.26.heavy)
        ;;
    *)
        echo "interp-model must be one of: 4.25.lite, 4.25, 4.26.heavy" >&2
        exit 2
        ;;
esac

case "$cq" in
    ''|*[!0-9]*)
        echo "cq must be a positive integer" >&2
        exit 2
        ;;
esac

case "$maxrate_kbps" in
    ''|*[!0-9]*)
        echo "maxrate-kbps must be a positive integer" >&2
        exit 2
        ;;
esac

case "$bufsize_kbps" in
    ''|*[!0-9]*)
        echo "bufsize-kbps must be a positive integer" >&2
        exit 2
        ;;
esac

mkdir -p /workspace/cache/trt /workspace/cache/src

trt_cache_dir="/workspace/cache/trt"
src_cache_dir="/workspace/cache/src"

trt_cache_count="$(find "$trt_cache_dir" -maxdepth 1 -type f | wc -l | tr -d '[:space:]')"
src_cache_count="$(find "$src_cache_dir" -maxdepth 1 -type f | wc -l | tr -d '[:space:]')"

if [[ "$trt_cache_count" -gt 0 ]]; then
    echo "MTE_RIFE_TRT_CACHE=warm files=$trt_cache_count dir=$trt_cache_dir"
else
    echo "MTE_RIFE_TRT_CACHE=cold files=0 dir=$trt_cache_dir"
fi

echo "MTE_RIFE_SOURCE_CACHE files=$src_cache_count dir=$src_cache_dir"

script_path="$(mktemp /tmp/media-transcode-rife-XXXXXX.vpy)"
pipe_dir="$(mktemp -d /tmp/media-transcode-rife-pipe-XXXXXX)"
pipe_path="$pipe_dir/video.y4m"
mkfifo "$pipe_path"

vspipe_pid=""
ffmpeg_pid=""

terminate_children() {
    local pid
    for pid in "$ffmpeg_pid" "$vspipe_pid"; do
        if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
            kill "$pid" 2>/dev/null || true
        fi
    done

    for pid in "$ffmpeg_pid" "$vspipe_pid"; do
        if [[ -n "$pid" ]]; then
            wait "$pid" 2>/dev/null || true
        fi
    done
}

cleanup() {
    terminate_children
    rm -f "$script_path"
    rm -f "$pipe_path"
    rmdir "$pipe_dir" 2>/dev/null || true
}

on_interrupt() {
    echo "Interrupted: stopping RIFE pipeline..." >&2
    terminate_children
    exit 130
}
trap on_interrupt INT TERM
trap cleanup EXIT

cat >"$script_path" <<EOF
import vapoursynth as vs
import vsrife

core = vs.core
clip = core.bs.VideoSource(
    source="$input_path",
    cachemode=1,
    cachepath="$src_cache_dir"
)
clip = core.resize.Bicubic(clip, format=vs.RGBH, matrix_in_s="709")
clip = vsrife.rife(
    clip,
    model="$interp_model",
    factor_num=$fps_multiplier,
    factor_den=1,
    trt=True,
    trt_cache_dir="$trt_cache_dir"
)
clip = core.resize.Bicubic(clip, format=vs.YUV420P8, matrix_s="709")
clip.set_output()
EOF

ffmpeg_args=(
    -hide_banner
    -y
    -f yuv4mpegpipe
    -i -
    -i "$input_path"
    -map 0:v:0
    -c:v h264_nvenc
    -preset p6
    -rc vbr
    -tune hq
    -multipass fullres
    -cq "$cq"
    -b:v 0
    -maxrate "${maxrate_kbps}k"
    -bufsize "${bufsize_kbps}k"
    -spatial_aq 1
    -temporal_aq 1
    -rc-lookahead 32
    -pix_fmt yuv420p
    -map 1:a?
    -c:a copy
    -sn
    -max_muxing_queue_size 4096
)

if [[ "${container_name,,}" == "mp4" ]]; then
    ffmpeg_args+=(-movflags +faststart)
fi

vspipe -c y4m "$script_path" - >"$pipe_path" &
vspipe_pid="$!"

ffmpeg "${ffmpeg_args[@]}" "$output_path" <"$pipe_path" &
ffmpeg_pid="$!"

set +e
wait "$ffmpeg_pid"
ffmpeg_status=$?
wait "$vspipe_pid"
vspipe_status=$?
set -e

if (( ffmpeg_status != 0 )); then
    exit "$ffmpeg_status"
fi

if (( vspipe_status != 0 )); then
    exit "$vspipe_status"
fi

echo "MTE_RIFE_TRT_CACHE_FILES_BEGIN"
find "$trt_cache_dir" -maxdepth 1 -type f -printf '%f\t%s\n' | sort
echo "MTE_RIFE_TRT_CACHE_FILES_END"
