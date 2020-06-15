FROM nginx:1.19.0 AS build
WORKDIR /src
RUN apt-get update && apt-get install -y git wget build-essential libssl-dev libpcre3-dev zlib1g-dev
RUN CONFARGS=$(nginx -V 2>&1 | sed -n -e 's/^.*arguments: //p') \
    git clone https://github.com/google/ngx_brotli.git && \
    cd ngx_brotli && git submodule update --init && cd .. && \
    wget -nv https://nginx.org/download/nginx-1.19.0.tar.gz -O - | tar -xz && \
    cd nginx-1.19.0 && \ 
    ./configure --with-compat $CONFARGS --add-dynamic-module=../ngx_brotli
WORKDIR nginx-1.19.0
RUN    make modules

FROM nginx:1.19.0
ARG ENVIRONMENT=
COPY --from=build /src/nginx-1.19.0/objs/ngx_http_brotli_filter_module.so /usr/lib/nginx/modules/
COPY --from=build /src/nginx-1.19.0/objs/ngx_http_brotli_static_module.so /usr/lib/nginx/modules/
COPY nginx.conf /etc/nginx/nginx.conf
WORKDIR /var/www/web
COPY /bin/$ENVIRONMENT/publish/wwwroot .