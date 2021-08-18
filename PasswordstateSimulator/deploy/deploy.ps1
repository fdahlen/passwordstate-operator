$namespace = "passwordstate-simulator"
$image = "ghcr.io/fdahlen/passwordstate-operator-simulator:latest"
$url = "http://passwordstatesimulator.passwordstate-simulator.svc.cluster.local"

#$image = "nginxdemos/hello:plain-text"

(Get-Content deployment.yaml).
    replace('%%image%%', $image) |
    replace('%%url%%', $url) |
    kubectl apply -n $namespace -f -

(Get-Content service.yaml) |
    kubectl apply -n $namespace -f -

# passwordstatesimulator.passwordstate-simulator.svc.cluster.local/api/passwords/123
# kubectl run -i --tty --rm debug --image=busybox --restart=Never -- sh