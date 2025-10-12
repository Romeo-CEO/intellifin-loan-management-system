{{- define "observability.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "observability.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name (include "observability.name" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{- define "observability.labels" -}}
app.kubernetes.io/name: {{ include "observability.name" . }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
{{- end -}}

{{- define "observability.selectorLabels" -}}
app.kubernetes.io/name: {{ include "observability.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{- define "observability.jaeger.selectorLabels" -}}
app.kubernetes.io/component: jaeger
{{ include "observability.selectorLabels" . }}
{{- end -}}

{{- define "observability.otel.selectorLabels" -}}
app.kubernetes.io/component: otel-collector
{{ include "observability.selectorLabels" . }}
{{- end -}}

{{- define "observability.loki.selectorLabels" -}}
app.kubernetes.io/component: loki
{{ include "observability.selectorLabels" . }}
{{- end -}}

{{- define "observability.promtail.selectorLabels" -}}
app.kubernetes.io/component: promtail
{{ include "observability.selectorLabels" . }}
{{- end -}}

{{- define "observability.monitoring.labels" -}}
app.kubernetes.io/component: monitoring
{{ include "observability.labels" . }}
{{- end -}}

{{- define "observability.monitoring.selectorLabels" -}}
app.kubernetes.io/component: monitoring
{{ include "observability.selectorLabels" . }}
{{- end -}}
