# ArgoCD Installation Values

The `argocd-values.yaml` file supplies the Helm configuration used to install ArgoCD into the `argocd` namespace.  It enables TLS ingress, OIDC integration with Keycloak, Slack/email notifications, and Prometheus ServiceMonitor resources so Story 1.25 acceptance criteria can be validated.

Install ArgoCD with:

```bash
helm repo add argo https://argoproj.github.io/argo-helm
helm repo update
kubectl create namespace argocd
helm upgrade --install argocd argo/argo-cd \
  --namespace argocd \
  --values argocd-values.yaml
```
