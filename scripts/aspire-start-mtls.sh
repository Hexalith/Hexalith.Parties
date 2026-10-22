#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_directory}/.." && pwd)"
session_id="${DAPR_MTLS_SESSION_ID:-}"
certificate_directory="${DAPR_MTLS_CERTIFICATE_DIRECTORY:-${XDG_STATE_HOME:-${HOME}/.local/state}/hexalith-parties/dapr-certs${session_id:+-${session_id}}}"
namespace="${DAPR_MTLS_NAMESPACE:-default}"
name_suffix="${session_id:+-${session_id}}"
sentry_port="${DAPR_MTLS_SENTRY_PORT:-50001}"
sentry_health_port="${DAPR_MTLS_SENTRY_HEALTH_PORT:-18080}"
placement_port="${DAPR_MTLS_PLACEMENT_PORT:-55005}"
placement_health_port="${DAPR_MTLS_PLACEMENT_HEALTH_PORT:-18081}"
scheduler_port="${DAPR_MTLS_SCHEDULER_PORT:-55006}"
scheduler_health_port="${DAPR_MTLS_SCHEDULER_HEALTH_PORT:-18082}"
sentry_container="hexalith-parties-dapr-sentry${name_suffix}"
sentry_image="daprio/dapr@sha256:b42eeb03c4300938226b7a5d7a15db5513e69e1d55570967c290d670c7612df2"
sentry_configuration="${repository_root}/src/Hexalith.Parties.AppHost/DaprComponents/sentry.yaml"
sentry_grpc_binding="127.0.0.1:${sentry_port}"
sentry_health_binding="127.0.0.1:${sentry_health_port}"
placement_container="hexalith-parties-dapr-placement${name_suffix}"
placement_grpc_binding="127.0.0.1:${placement_port}"
placement_health_binding="127.0.0.1:${placement_health_port}"
scheduler_container="hexalith-parties-dapr-scheduler${name_suffix}"
scheduler_grpc_binding="127.0.0.1:${scheduler_port}"
scheduler_health_binding="127.0.0.1:${scheduler_health_port}"
control_plane_network="hexalith-parties-dapr-mtls${name_suffix}"
prepare_only=false
cleanup_only=false

if [[ "${1:-}" == "--prepare-only" ]]; then
    prepare_only=true
    shift
    if (( $# != 0 )); then
        echo "--prepare-only does not accept Aspire arguments." >&2
        exit 2
    fi
elif [[ "${1:-}" == "--cleanup-only" ]]; then
    cleanup_only=true
    shift
    if (( $# != 0 )); then
        echo "--cleanup-only does not accept Aspire arguments." >&2
        exit 2
    fi
fi

if [[ ( -n "${session_id}" && ! "${session_id}" =~ ^[a-z0-9-]{1,40}$ ) || ! "${namespace}" =~ ^[a-z0-9-]{1,63}$ ]]; then
    echo "Dapr mTLS session identity or namespace is invalid." >&2
    exit 2
fi
if [[ -z "${session_id}" && "${sentry_port}" != 50001 ]]; then
    echo "A custom Sentry port requires DAPR_MTLS_SESSION_ID." >&2
    exit 2
fi
if [[ -z "${session_id}" && "${namespace}" != default ]]; then
    echo "A custom Dapr namespace requires a session identity and app-ID suffix." >&2
    exit 2
fi
for port in "${sentry_port}" "${sentry_health_port}" "${placement_port}" "${placement_health_port}" "${scheduler_port}" "${scheduler_health_port}"; do
    if [[ ! "${port}" =~ ^[0-9]{4,5}$ ]] || (( 10#${port} < 1024 || 10#${port} > 65535 )); then
        echo "Dapr mTLS ports must be unprivileged loopback ports." >&2
        exit 2
    fi
done
if [[ "$(printf '%s\n' "${sentry_port}" "${sentry_health_port}" "${placement_port}" "${placement_health_port}" "${scheduler_port}" "${scheduler_health_port}" | sort -u | wc -l)" != 6 ]]; then
    echo "Dapr mTLS listener ports must be distinct." >&2
    exit 2
fi

read_configured_port_binding() {
    local container_name="$1"
    local container_port="$2"

    # Docker's runtime port listing is empty while a container is stopped. Inspect
    # its persistent bindings before deciding whether the existing container is safe to start.
    docker container inspect \
        --format "{{range (index .HostConfig.PortBindings \"${container_port}/tcp\")}}{{.HostIp}}:{{.HostPort}}{{println}}{{end}}" \
        "${container_name}"
}

attached_control_plane_networks() {
    local container_name="$1"
    docker container inspect \
        --format '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}' \
        "${container_name}"
}

verify_control_plane_network() {
    local driver
    local label
    local member
    local members
    driver="$(docker network inspect --format '{{.Driver}}' "${control_plane_network}")"
    label="$(docker network inspect --format '{{index .Labels "hexalith.parties.dapr.role"}}' "${control_plane_network}")"
    if [[ "${driver}" != "bridge" || "${label}" != "control-plane-v1" ]]; then
        echo "Existing ${control_plane_network} is not the pinned control-plane bridge." >&2
        exit 1
    fi

    members="$(docker network inspect --format '{{range .Containers}}{{println .Name}}{{end}}' "${control_plane_network}")"
    while IFS= read -r member; do
        case "${member}" in
            ""|"${sentry_container}"|"${placement_container}"|"${scheduler_container}") ;;
            *)
                echo "Existing ${control_plane_network} contains unrelated container '${member}'." >&2
                exit 1 ;;
        esac
    done <<< "${members}"
}

cleanup_owned_control_plane() {
    local container_name
    local container_names
    local network_names
    local credentials_destination
    local configured_image
    local configured_mount
    local network_label
    local network_members

    # An unsuccessful inspect cannot distinguish absence from a daemon/transport
    # failure. Enumerate first and let every enumeration or inspection error fail
    # cleanup so the fixture retains its credentials and ownership evidence.
    container_names="$(docker container ls --all --format '{{.Names}}')"
    network_names="$(docker network ls --format '{{.Name}}')"
    for container_name in "${scheduler_container}" "${placement_container}" "${sentry_container}"; do
        if [[ $'\n'"${container_names}"$'\n' != *$'\n'"${container_name}"$'\n'* ]]; then
            continue
        fi

        credentials_destination="/var/run/secrets/dapr.io/tls"
        if [[ "${container_name}" == "${sentry_container}" ]]; then
            credentials_destination="/var/run/dapr/credentials"
        fi
        configured_image="$(docker container inspect --format '{{.Config.Image}}' "${container_name}")"
        configured_mount="$(docker container inspect \
            --format "{{range .Mounts}}{{if eq .Destination \"${credentials_destination}\"}}{{.Source}}{{end}}{{end}}" \
            "${container_name}")"
        if [[ "${configured_image}" == "${sentry_image}" \
            && "${configured_mount}" == "${certificate_directory}" ]]; then
            docker rm -f "${container_name}" >/dev/null
        elif [[ -n "${session_id}" ]]; then
            echo "Owned session container ${container_name} has conflicting ownership; retain session evidence." >&2
            return 1
        fi
    done

    if [[ -f "${certificate_directory}/.owned-control-plane-network" \
        && $'\n'"${network_names}"$'\n' == *$'\n'"${control_plane_network}"$'\n'* ]]; then
        network_label="$(docker network inspect --format '{{index .Labels "hexalith.parties.dapr.role"}}' "${control_plane_network}")"
        network_members="$(docker network inspect --format '{{len .Containers}}' "${control_plane_network}")"
        if [[ "${network_label}" == "control-plane-v1" && "${network_members}" == "0" ]]; then
            docker network rm "${control_plane_network}" >/dev/null
        elif [[ -n "${session_id}" ]]; then
            echo "Owned session network ${control_plane_network} has conflicting ownership or active members; retain session evidence." >&2
            return 1
        fi
    fi
}

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
    configured_grpc_binding="$(read_configured_port_binding "${container_name}" "${grpc_container_port}")"
    configured_health_binding="$(read_configured_port_binding "${container_name}" 8080)"

    if [[ "${configured_role}" != "${expected_role}" \
        || "${configured_image}" != "${sentry_image}" \
        || "${configured_credentials_mount}" != "${certificate_directory}" \
        || "${configured_grpc_binding}" != "${expected_grpc_binding}" \
        || "${configured_health_binding}" != "${expected_health_binding}" \
        || "$(attached_control_plane_networks "${container_name}")" != "${control_plane_network}" ]]; then
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

wait_for_control_plane_port() {
    local container_name="$1"
    local port="$2"
    for _ in {1..30}; do
        if timeout 1 bash -c 'exec 3<>/dev/tcp/127.0.0.1/$1' _ "${port}" >/dev/null 2>&1; then
            return 0
        fi
        sleep 0.5
    done

    docker container logs --tail 50 "${container_name}" >&2
    echo "${container_name} did not expose loopback gRPC port ${port}." >&2
    exit 1
}

if [[ -z "${certificate_directory}" || "${certificate_directory}" != /* || "${certificate_directory}" == "/" || "${certificate_directory}" == "${HOME}" ]]; then
    echo "DAPR_MTLS_CERTIFICATE_DIRECTORY must be an absolute, dedicated directory." >&2
    exit 1
fi

if [[ "${cleanup_only}" == "true" ]]; then
    cleanup_owned_control_plane
    exit 0
fi

install -d -m 0700 -- "${certificate_directory}"
if ! docker network inspect "${control_plane_network}" >/dev/null 2>&1; then
    docker network create --driver bridge \
        --label hexalith.parties.dapr.role=control-plane-v1 \
        "${control_plane_network}" >/dev/null
    touch "${certificate_directory}/.owned-control-plane-network"
fi
verify_control_plane_network
if [[ ! -f "${sentry_configuration}" ]]; then
    echo "Dapr Sentry configuration '${sentry_configuration}' was not found." >&2
    exit 1
fi

if docker container inspect "${sentry_container}" >/dev/null 2>&1; then
    configured_image="$(docker container inspect --format '{{.Config.Image}}' "${sentry_container}")"
    configured_mount="$(docker container inspect --format '{{range .Mounts}}{{if eq .Destination "/var/run/dapr/credentials"}}{{.Source}}{{end}}{{end}}' "${sentry_container}")"
    configured_sentry_configuration="$(docker container inspect --format '{{range .Mounts}}{{if eq .Destination "/etc/dapr/sentry.yaml"}}{{.Source}}{{end}}{{end}}' "${sentry_container}")"
    configured_grpc_binding="$(read_configured_port_binding "${sentry_container}" 50001)"
    configured_health_binding="$(read_configured_port_binding "${sentry_container}" 18080)"
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

    if [[ "${configured_grpc_binding}" != "${sentry_grpc_binding}" \
        || "${configured_health_binding}" != "${sentry_health_binding}" \
        || "$(attached_control_plane_networks "${sentry_container}")" != "${control_plane_network}" ]]; then
        echo "Existing ${sentry_container} does not publish the required loopback-only Sentry ports." >&2
        exit 1
    fi

    if [[ "$(docker container inspect --format '{{.State.Running}}' "${sentry_container}")" != "true" ]]; then
        docker container start "${sentry_container}" >/dev/null
    fi
else
    docker run --detach \
        --name "${sentry_container}" \
        --network "${control_plane_network}" \
        --publish "${sentry_grpc_binding}:50001" \
        --publish "${sentry_health_binding}:18080" \
        --restart no \
        --env "NAMESPACE=${namespace}" \
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

wait_for_control_plane_health "${sentry_container}" "${sentry_health_port}"

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
        --network "${control_plane_network}" \
        --label "hexalith.parties.dapr.role=placement-v1" \
        --publish "${placement_grpc_binding}:50005" \
        --publish "${placement_health_binding}:8080" \
        --restart no \
        --env "NAMESPACE=${namespace}" \
        --user "$(id -u):$(id -g)" \
        --env "DAPR_CONTROLPLANE_NAMESPACE=${namespace}" \
        --mount "type=bind,source=${certificate_directory},target=/var/run/secrets/dapr.io/tls,readonly" \
        "${sentry_image}" \
        ./placement \
        --tls-enabled \
        --sentry-address "${sentry_container}:50001" \
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
        --network "${control_plane_network}" \
        --label "hexalith.parties.dapr.role=scheduler-v2" \
        --publish "${scheduler_grpc_binding}:50006" \
        --publish "${scheduler_health_binding}:8080" \
        --restart no \
        --env "NAMESPACE=${namespace}" \
        --user "$(id -u):$(id -g)" \
        --env "DAPR_CONTROLPLANE_NAMESPACE=${namespace}" \
        --mount "type=bind,source=${certificate_directory},target=/var/run/secrets/dapr.io/tls,readonly" \
        "${sentry_image}" \
        ./scheduler \
        --tls-enabled \
        --sentry-address "${sentry_container}:50001" \
        --trust-domain public \
        --trust-anchors-file /var/run/secrets/dapr.io/tls/ca.crt \
        --listen-address 0.0.0.0 \
        --healthz-listen-address 0.0.0.0 \
        --port 50006 \
        --override-broadcast-host-port "127.0.0.1:${scheduler_port}" \
        --etcd-data-dir /tmp/dapr-scheduler \
        --etcd-client-listen-address 127.0.0.1 \
        --etcd-initial-cluster dapr-scheduler-server-0=http://127.0.0.1:2380 \
        --enable-metrics=false >/dev/null
fi

wait_for_control_plane_health "${placement_container}" "${placement_health_port}"
wait_for_control_plane_health "${scheduler_container}" "${scheduler_health_port}"
wait_for_control_plane_port "${sentry_container}" "${sentry_port}"
wait_for_control_plane_port "${placement_container}" "${placement_port}"
wait_for_control_plane_port "${scheduler_container}" "${scheduler_port}"

export Dapr__Mtls__Enabled=true
export Dapr__Mtls__CertificateDirectory="${certificate_directory}"
export Dapr__Mtls__Namespace="${namespace}"
export Dapr__Mtls__PlacementHostAddress="127.0.0.1:${placement_port}"
export Dapr__Mtls__SchedulerHostAddress="127.0.0.1:${scheduler_port}"
if [[ -n "${session_id}" ]]; then
    export Dapr__Mtls__SessionId="${session_id}"
    export Dapr__Mtls__SentryHostAddress="127.0.0.1:${sentry_port}"
    export Dapr__Mtls__ConfigurationDirectory="${certificate_directory}/configuration"
    export Dapr__Mtls__AppIdSuffix="${session_id}"
fi

if [[ "${prepare_only}" == "true" ]]; then
    exit 0
fi

cd -- "${repository_root}"
exec aspire start \
    --apphost "${repository_root}/src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj" \
    --isolated \
    --non-interactive \
    "$@"
