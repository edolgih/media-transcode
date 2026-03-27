FROM styler00dollar/vsgan_tensorrt:latest_no_avx512

LABEL org.opencontainers.image.title="media-transcode-rife-trt"
LABEL org.opencontainers.image.description="Thin wrapper image for MediaTranscodeEngine RIFE TRT backend"

WORKDIR /workspace

RUN mkdir -p /opt/media-transcode

COPY run-rife-trt.sh /opt/media-transcode/run-rife-trt.sh

RUN chmod +x /opt/media-transcode/run-rife-trt.sh

# Base image updates can introduce torch/torchvision CUDA ABI mismatch.
# Reinstall torchvision from the same CUDA wheel index as the bundled torch.
RUN python - <<'PY'
import subprocess
import sys
from importlib.metadata import PackageNotFoundError, version

import torch

cuda_version = torch.version.cuda
if not cuda_version:
    raise RuntimeError("Expected CUDA-enabled torch in base image, but torch.version.cuda is empty.")

cuda_tag = f"cu{cuda_version.replace('.', '')}"
index_url = f"https://download.pytorch.org/whl/{cuda_tag}"

try:
    torchvision_base = version("torchvision").split("+", 1)[0]
except PackageNotFoundError as exc:
    raise RuntimeError("torchvision is not installed in base image.") from exc

install_cmd = [
    sys.executable,
    "-m",
    "pip",
    "install",
    "--no-cache-dir",
    "--force-reinstall",
    "--no-deps",
    "--index-url",
    index_url,
    f"torchvision=={torchvision_base}",
]
print("Reinstalling torchvision from:", index_url)
subprocess.check_call(install_cmd)

import torchvision  # noqa: E402
print("torch:", torch.__version__)
print("torch cuda:", torch.version.cuda)
print("torchvision:", torchvision.__version__)
PY

ENTRYPOINT ["/opt/media-transcode/run-rife-trt.sh"]
