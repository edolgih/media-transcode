FROM styler00dollar/vsgan_tensorrt:latest_no_avx512

LABEL org.opencontainers.image.title="media-transcode-rife-trt"
LABEL org.opencontainers.image.description="Thin wrapper image for MediaTranscodeEngine RIFE TRT backend"

WORKDIR /workspace

RUN mkdir -p /opt/media-transcode

COPY run-rife-trt.sh /opt/media-transcode/run-rife-trt.sh

RUN chmod +x /opt/media-transcode/run-rife-trt.sh

ENTRYPOINT ["/opt/media-transcode/run-rife-trt.sh"]
