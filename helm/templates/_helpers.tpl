
{{- define "azure-keyvault-secret-operator.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}


{{- define "azure-keyvault-secret-operator.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}


{{- define "azure-keyvault-secret-operator.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}


{{- define "azure-keyvault-secret-operator.labels" -}}
helm.sh/chart: {{ include "azure-keyvault-secret-operator.chart" . }}
{{ include "azure-keyvault-secret-operator.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}


{{- define "azure-keyvault-secret-operator.selectorLabels" -}}
app.kubernetes.io/name: {{ include "azure-keyvault-secret-operator.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}


{{- define "azure-keyvault-secret-operator.serviceAccountName" -}}
{{ include "azure-keyvault-secret-operator.fullname" . }}
{{- end }}

{{- define "azure-keyvault-secret-operator.clusterRoleName" -}}
{{ include "azure-keyvault-secret-operator.fullname" . }}
{{- end }}