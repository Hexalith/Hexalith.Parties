#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_directory}/.." && pwd)"
certificate_directory="${DAPR_MTLS_CERTIFICATE_DIRECTORY:-${XDG_STATE_HOME:-${HOME}/.local/state}/hexalith-parties/dapr-certs}"
sentry_container="hexalith-parties-dapr-sentry"
sentry_image="daprio/dapr@sha256:b42eeb03c4300938226b7a5d7a15db5513e69e1d55570967c290d670c7612df2"
sentry_configuration="${repository_root}/src/Hexalith.Parties.AppHost/DaprComponents/sentry.yaml"
sentry_grpc_binding="127.0.0.1:50001"
sentry_health_binding="127.0.0.1:18080"
placement_container="hexalith-parties-dapr-placement"
placement_grpc_binding="127.0.0.1:55005"
placement_health_binding="127.0.0.1:18081"
scheduler_container="hexalith-parties-dapr-scheduler"
scheduler_grpc_binding="127.0.0.1:55006"
scheduler_health_binding="127.0.0.1:18082"

verify_control_plane_container() {
    local container_name="$1"
    local expected_role="$2"
    local grpc_container_port="$3"
    local expected_grpc_binding="$4"
    local expected_health_binding="$5"
    local configured_role
    local configured_image
    local configured_credentials_mount
    local configured_grpc_binding
    local configured_health_binding

    configured_role="$(docker container inspect --format '{{index .Config.Labels "hexalith.parties.dapr.role"}}' "${container_name}")"
    configured_image="$(docker container inspect --format '{{.Config.Image}}' "${container_name}")"
    configured_credentials_mount="$(docker container inspect --format '{{range .Mounts}}{{if eq .Destination "/var/run/secrets/dapr.io/tls"}}{{.Source}}{{end}}{{end}}' "${container_name}")"
    configured_grpc_binding="$(docker port "${container_name}" "${grpc_container_port}/tcp" 2>/dev/null || true)"
    configured_health_binding="$(docker port "${container_name}" 8080/tcp 2>/dev/null || true)"

    if [[ "${configured_role}" != "${expected_role}" \
        || "${configured_image}" != "${sentry_image}" \
        || "${configured_credentials_mount}" != "${certificate_directory}" \
        || "${configured_grpc_binding}" != "${expected_grpc_binding}" \
        || "${configured_health_binding}" != "${expected_health_binding}" ]]; then
        echo "Existing ${container_name} does not match the pinned ${expected_role} topology." >&2
        exit 1
    fi
}

start_if_stopped() {
    local container_name="$1"
    if [[ "$(docker container inspect --format '{{.State.Running}}' "${container_name}")" != "true" ]]; then
        docker container start "${container_name}" >/dev/null
    fi
}

wait_for_control_plane_health() {
    local container_name="$1"
    local health_port="$2"
    local healthy=false
    for _ in {1..60}; do
        if [[ "$(docker container inspect --format '{{.State.Running}}' "${container_name}")" != "true" ]]; then
            docker container logs --tail 50 "${container_name}" >&2
            echo "${container_name} stopped before becoming healthy." >&2
            exit 1
        fi

        if curl --fail --silent --show-error --max-time 1 "http://127.0.0.1:${health_port}/healthz" >/dev/null 2>&1; then
            healthy=true
            break
        fi

        sleep 0.5
    done

    if [[ "${healthy}" != "true" ]]; then
        docker container logs --tail 50 "${container_name}" >&2
        echo "${container_name} did not become healthy within 30 seconds." >&2
        exit 1
    fi
}

if [[ -z "${certificate_directory}" || "${certificate_directory}" != /* || "${certificate_directory}" == "/" || "${certificate_directory}" == "${HOME}" ]]; then
    echo "DAPR_MTLS_CERTIFICATE_DIRECTORY must be an absolute, dedicated directory." >&2
    exit 1
fi

install -d -m 0700 -- "${certificate_directory}"
if [[ ! -f "${sentry_configuration}" ]]; then
    echo "Dapr Sentry configuration '${sentry_configuration}' was not found." >&2
    exit 1
fi

if docker container inspect "${sentry_container}" >/dev/null 2>&1; then
    configured_image="$(docker container inspect --format '{{.Config.Image}}' "${sentry_container}")"
    configured_mount="$(docker container inspect --format '{{range .Mounts}}{{if eq .Destination "/var/run/dapr/credentials"}}{{.Source}}{{end}}{{end}}' "${sentry_container}")"
    configured_sentry_configuration="$(docker container inspect --format '{{range .Mounts}}{{if eq .Destination "/etc/dapr/sentry.yaml"}}{{.Source}}{{end}}{{end}}' "${sentry_container}")"
    configured_grpc_binding="$(docker port "${sentry_container}" 50001/tcp 2>/dev/null || true)"
    configured_health_binding="$(docker port "${sentry_container}" 18080/tcp 2>/dev/null || true)"
    if [[ "${configured_image}" != "${sentry_image}" ]]; then
        echo "Existing ${sentry_container} uses '${configured_image}', expected '${sentry_image}'." >&2
        exit 1
    fi

    if [[ "${configured_mount}" != "${certificate_directory}" ]]; then
        echo "Existing ${sentry_container} mounts '${configured_mount}', expected '${certificate_directory}'." >&2
        exit 1
    fi

    if [[ "${configured_sentry_configuration}" != "${sentry_configuration}" ]]; then
        echo "Existing ${sentry_container} mounts Sentry config '${configured_sentry_configuration}', expected '${sentry_configuration}'." >&2
        exit 1
    fi

    if [[ "${configured_grpc_binding}" != "${sentry_grpc_binding}" || "${configured_health_binding}" != "${sentry_health_binding}" ]]; then
        echo "Existing ${sentry_container} does not publish the required loopback-only Sentry ports." >&2
        exit 1
    fi

    if [[ "$(docker container inspect --format '{{.State.Running}}' "${sentry_container}")" != "true" ]]; then
        docker container start "${sentry_container}" >/dev/null
    fi
else
    docker run --detach \
        --name "${sentry_container}" \
        --publish "${sentry_grpc_binding}:50001" \
        --publish "${sentry_health_binding}:18080" \
        --restart no \
        --user "$(id -u):$(id -g)" \
        --mount "type=bind,source=${certificate_directory},target=/var/run/dapr/credentials" \
        --mount "type=bind,source=${sentry_configuration},target=/etc/dapr/sentry.yaml,readonly" \
        "${sentry_image}" \
        ./sentry \
        --config /etc/dapr/sentry.yaml \
        --issuer-credentials /var/run/dapr/credentials \
        --trust-domain public \
        --mode standalone \
        --listen-address 0.0.0.0 \
        --healthz-listen-address 0.0.0.0 \
        --healthz-port 18080 \
        --enable-metrics=false >/dev/null
fi

credentials_ready=false
for _ in {1..60}; do
    if [[ "$(docker container inspect --format '{{.State.Running}}' "${sentry_container}")" != "true" ]]; then
        docker container logs --tail 50 "${sentry_container}" >&2
        echo "Dapr Sentry stopped before issuing its trust bundle." >&2
        exit 1
    fi

    if [[ -s "${certificate_directory}/ca.crt" \
        && -s "${certificate_directory}/issuer.crt" \
        && -s "${certificate_directory}/issuer.key" ]]; then
        credentials_ready=true
        break
    fi

    sleep 0.5
done

if [[ "${credentials_ready}" != "true" ]]; then
    docker container logs --tail 50 "${sentry_container}" >&2
    echo "Dapr Sentry did not issue its trust bundle within 30 seconds." >&2
    exit 1
fi

wait_for_control_plane_health "${sentry_container}" 18080

if docker container inspect "${placement_container}" >/dev/null 2>&1; then
    verify_control_plane_container \
        "${placement_container}" \
        "placement-v1" \
        50005 \
        "${placement_grpc_binding}" \
        "${placement_health_binding}"
    start_if_stopped "${placement_container}"
else
    docker run --detach \
        --name "${placement_container}" \
        --label "hexalith.parties.dapr.role=placement-v1" \
        --publish "${placement_grpc_binding}:50005" \
        --publish "${placement_health_binding}:8080" \
        --restart no \
        --user "$(id -u):$(id -g)" \
        --add-host host.docker.internal:host-gateway \
        --env DAPR_CONTROLPLANE_NAMESPACE=default \
        --mount "type=bind,source=${certificate_directory},target=/var/run/secrets/dapr.io/tls,readonly" \
        "${sentry_image}" \
        ./placement \
        --tls-enabled \
        --sentry-address host.docker.internal:50001 \
        --trust-domain public \
        --listen-address 0.0.0.0 \
        --healthz-listen-address 0.0.0.0 \
        --port 50005 \
        --enable-metrics=false >/dev/null
fi

if docker container inspect "${scheduler_container}" >/dev/null 2>&1; then
    verify_control_plane_container \
        "${scheduler_container}" \
        "scheduler-v2" \
        50006 \
        "${scheduler_grpc_binding}" \
        "${scheduler_health_binding}"
    start_if_stopped "${scheduler_container}"
else
    docker run --detach \
        --name "${scheduler_container}" \
        --label "hexalith.parties.dapr.role=scheduler-v2" \
        --publish "${scheduler_grpc_binding}:50006" \
        --publish "${scheduler_health_binding}:8080" \
        --restart no \
        --user "$(id -u):$(id -g)" \
        --add-host host.docker.internal:host-gateway \
        --env DAPR_CONTROLPLANE_NAMESPACE=default \
        --mount "type=bind,source=${certificate_directory},target=/var/run/secrets/dapr.io/tls,readonly" \
        "${sentry_image}" \
        ./scheduler \
        --tls-enabled \
        --sentry-address host.docker.internal:50001 \
        --trust-domain public \
        --trust-anchors-file /var/run/secrets/dapr.io/tls/ca.crt \
        --listen-address 0.0.0.0 \
        --healthz-listen-address 0.0.0.0 \
        --port 50006 \
        --override-broadcast-host-port 127.0.0.1:55006 \
        --etcd-data-dir /tmp/dapr-scheduler \
        --etcd-client-listen-address 127.0.0.1 \
        --etcd-initial-cluster dapr-scheduler-server-0=http://127.0.0.1:2380 \
        --enable-metrics=false >/dev/null
fi

wait_for_control_plane_health "${placement_container}" 18081
wait_for_control_plane_health "${scheduler_container}" 18082

export Dapr__Mtls__Enabled=true
export Dapr__Mtls__CertificateDirectory="${certificate_directory}"

cd -- "${repository_root}"
exec aspire start \
    --apphost "${repository_root}/src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj" \
    --isolated \
    --non-interactive \
    "$@"
